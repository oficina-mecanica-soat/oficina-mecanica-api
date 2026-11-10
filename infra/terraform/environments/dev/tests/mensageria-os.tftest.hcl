mock_provider "aws" {
  override_during = plan
  mock_data "aws_ssm_parameter" {
    defaults = { value = "ready" }
  }
}
mock_provider "kubernetes" {}
variables {
  aws_region           = "us-east-1"
  ssm_environment_name = "development"
  db_connection_string = "somente-teste-local-sem-credencial-real"
  api_deploy_enabled   = false
}
override_resource {
  target          = aws_sqs_queue.os_entrada
  override_during = plan
  values = {
    arn = "arn:aws:sqs:us-east-1:123456789012:oficina-mecanica-api-development.fifo"
    url = "https://sqs.us-east-1.amazonaws.com/123456789012/oficina-mecanica-api-development.fifo"
  }
}
override_resource {
  target          = aws_sqs_queue.os_dlq
  override_during = plan
  values = {
    arn = "arn:aws:sqs:us-east-1:123456789012:oficina-mecanica-api-development-dlq.fifo"
    url = "https://sqs.us-east-1.amazonaws.com/123456789012/oficina-mecanica-api-development-dlq.fifo"
  }
}
run "parametros_fifo_e_redrive_conforme_rfc" {
  command = plan
  assert {
    condition = (aws_sqs_queue.os_entrada.fifo_queue && aws_sqs_queue.os_dlq.fifo_queue
      && !aws_sqs_queue.os_entrada.content_based_deduplication
      && aws_sqs_queue.os_entrada.visibility_timeout_seconds == 60
      && aws_sqs_queue.os_entrada.receive_wait_time_seconds == 20
      && aws_sqs_queue.os_entrada.message_retention_seconds == 345600
      && aws_sqs_queue.os_dlq.message_retention_seconds == 1209600
      && jsondecode(aws_sqs_queue.os_entrada.redrive_policy).maxReceiveCount == 5
    && jsondecode(aws_sqs_queue.os_entrada.redrive_policy).deadLetterTargetArn == aws_sqs_queue.os_dlq.arn)
    error_message = "Entrada e DLQ de OS devem preservar os parâmetros da RFC-0007."
  }
  assert {
    condition = (jsondecode(aws_sqs_queue_redrive_allow_policy.os_dlq.redrive_allow_policy).redrivePermission == "byQueue"
    && jsondecode(aws_sqs_queue_redrive_allow_policy.os_dlq.redrive_allow_policy).sourceQueueArns == [aws_sqs_queue.os_entrada.arn])
    error_message = "A DLQ de OS deve receber redrive somente da própria entrada."
  }
}
run "contratos_ssm_correspondem_aos_recursos_do_owner" {
  command = plan
  assert {
    condition = (aws_ssm_parameter.os_mensageria["entrada_queue_url"].value == aws_sqs_queue.os_entrada.url
      && aws_ssm_parameter.os_mensageria["entrada_queue_arn"].value == aws_sqs_queue.os_entrada.arn
      && aws_ssm_parameter.os_mensageria["dlq_url"].value == aws_sqs_queue.os_dlq.url
      && aws_ssm_parameter.os_mensageria["dlq_arn"].value == aws_sqs_queue.os_dlq.arn
    && alltrue([for parametro in aws_ssm_parameter.os_mensageria : parametro.type == "String"]))
    error_message = "Os contratos devem publicar referências dos recursos de OS, sem credenciais."
  }
}
run "ambiente_parametriza_nomes_e_contratos" {
  command = plan
  variables {
    ssm_environment_name = "homologation"
  }
  assert {
    condition = (aws_sqs_queue.os_entrada.name == "oficina-mecanica-api-homologation.fifo"
      && aws_sqs_queue.os_dlq.name == "oficina-mecanica-api-homologation-dlq.fifo"
      && aws_ssm_parameter.os_mensageria["entrada_queue_url"].name == "/oficina-mecanica/homologation/api/entrada_queue_url"
    && aws_ssm_parameter.os_mensageria_status.name == "/oficina-mecanica/homologation/status/api")
    error_message = "Mensageria e contratos não podem apontar silenciosamente para development."
  }
}

locals {
  os_queue_prefix = "oficina-mecanica-api-${var.ssm_environment_name}"
  os_ssm_prefix   = "${local.ssm_base_path}/api"
  os_queue_tags = merge(local.common_tags, {
    Environment = var.ssm_environment_name
    Repository  = "oficina-mecanica-api"
  })
}

resource "aws_sqs_queue" "os_dlq" {
  name                        = "${local.os_queue_prefix}-dlq.fifo"
  fifo_queue                  = true
  content_based_deduplication = false
  message_retention_seconds   = 1209600
  sqs_managed_sse_enabled     = true
  tags                        = local.os_queue_tags
}

resource "aws_sqs_queue" "os_entrada" {
  name                        = "${local.os_queue_prefix}.fifo"
  fifo_queue                  = true
  content_based_deduplication = false
  visibility_timeout_seconds  = 60
  receive_wait_time_seconds   = 20
  message_retention_seconds   = 345600
  sqs_managed_sse_enabled     = true
  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.os_dlq.arn
    maxReceiveCount     = 5
  })
  tags = local.os_queue_tags
}

resource "aws_sqs_queue_redrive_allow_policy" "os_dlq" {
  queue_url = aws_sqs_queue.os_dlq.url
  redrive_allow_policy = jsonencode({
    redrivePermission = "byQueue"
    sourceQueueArns   = [aws_sqs_queue.os_entrada.arn]
  })
}

locals {
  os_messaging_contracts = {
    entrada_queue_url = aws_sqs_queue.os_entrada.url
    entrada_queue_arn = aws_sqs_queue.os_entrada.arn
    dlq_url           = aws_sqs_queue.os_dlq.url
    dlq_arn           = aws_sqs_queue.os_dlq.arn
  }
}

resource "aws_ssm_parameter" "os_mensageria" {
  for_each    = local.os_messaging_contracts
  name        = "${local.os_ssm_prefix}/${each.key}"
  description = "Contrato não sensível da mensageria de OS: ${each.key}."
  type        = "String"
  value       = each.value
  tags        = local.os_queue_tags
}

resource "aws_ssm_parameter" "os_mensageria_status" {
  name        = "${local.ssm_base_path}/status/api"
  description = "Disponibilidade da infraestrutura de mensageria de OS; não comprova saúde da API ou Saga."
  type        = "String"
  value       = "ready"
  depends_on  = [aws_ssm_parameter.os_mensageria, aws_sqs_queue_redrive_allow_policy.os_dlq]
  tags        = local.os_queue_tags
}

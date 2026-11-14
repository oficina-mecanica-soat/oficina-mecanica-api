mock_provider "aws" {
  mock_data "aws_ssm_parameter" {
    defaults = { value = "ready" }
  }
  mock_data "aws_eks_cluster" {
    defaults = {
      endpoint              = "https://127.0.0.1"
      certificate_authority = [{ data = "dGVzdGU=" }]
    }
  }
}
mock_provider "kubernetes" {}
override_resource {
  target          = aws_sqs_queue.os_entrada
  override_during = plan
  values = {
    url = "https://sqs.us-east-1.amazonaws.com/123456789012/oficina-mecanica-api-development.fifo"
  }
}

override_data {
  target = data.aws_ssm_parameter.kubernetes_api_internal_node_port
  values = { value = "30080" }
}

variables {
  aws_region           = "us-east-1"
  db_connection_string = "conexao-ficticia-somente-teste"
  api_deploy_enabled   = true
  api_image_uri        = "teste.local/api:teste"
}

run "consulta_privada_desabilitada_sem_novo_secret" {
  command = plan
  assert {
    condition = (kubernetes_config_map_v1.oficina_mecanica_api[0].data["Integracoes__ClientesInternos__Habilitado"] == "false"
    && !contains(keys(kubernetes_secret_v1.oficina_mecanica_api[0].data), "Integracoes__ClientesInternos__Token"))
    error_message = "A implantação atual não deve depender da nova credencial técnica."
  }
}

run "consulta_privada_habilitada_com_secret" {
  command = plan
  variables {
    clientes_internos_habilitado = true
    clientes_internos_token      = "credencial-ficticia-exclusiva-somente-teste"
  }
  assert {
    condition = (kubernetes_config_map_v1.oficina_mecanica_api[0].data["Integracoes__ClientesInternos__Habilitado"] == "true"
    && contains(keys(kubernetes_secret_v1.oficina_mecanica_api[0].data), "Integracoes__ClientesInternos__Token"))
    error_message = "A ativação deve encaminhar a credencial exclusivamente pelo Secret Kubernetes."
  }
}

run "credencial_privada_invalida_bloqueia" {
  command = plan
  variables {
    clientes_internos_habilitado = true
    clientes_internos_token      = "curta"
  }
  expect_failures = [var.clientes_internos_token]
}

run "corte_exige_workers_e_consulta_privada" {
  command = plan
  variables { fluxo_distribuido_habilitado = true }
  expect_failures = [var.fluxo_distribuido_habilitado]
}

run "workers_recusam_fila_de_outra_conta" {
  command = plan
  variables {
    processamento_remoto_habilitado       = true
    conta_aws                             = "123456789012"
    execucao_entrada_queue_url            = "https://sqs.us-east-1.amazonaws.com/999999999999/oficina-mecanica-execucao-development.fifo"
    orcamento_pagamento_entrada_queue_url = "https://sqs.us-east-1.amazonaws.com/123456789012/oficina-mecanica-orcamento-pagamento-development.fifo"
  }
  expect_failures = [var.processamento_remoto_habilitado]
}

run "modo_e_workers_publicam_contratos_consistentes" {
  command = plan
  variables {
    fluxo_distribuido_habilitado          = true
    clientes_internos_habilitado          = true
    clientes_internos_token               = "credencial-ficticia-exclusiva-somente-teste"
    processamento_remoto_habilitado       = true
    conta_aws                             = "123456789012"
    execucao_entrada_queue_url            = "https://sqs.us-east-1.amazonaws.com/123456789012/oficina-mecanica-execucao-development.fifo"
    orcamento_pagamento_entrada_queue_url = "https://sqs.us-east-1.amazonaws.com/123456789012/oficina-mecanica-orcamento-pagamento-development.fifo"
  }
  assert {
    condition = (
      kubernetes_config_map_v1.oficina_mecanica_api[0].data["Integracoes__FluxoDistribuido__Habilitado"] == "true" &&
      kubernetes_config_map_v1.oficina_mecanica_api[0].data["ProcessamentoRemoto__Habilitado"] == "true" &&
      kubernetes_config_map_v1.oficina_mecanica_api[0].data["ProcessamentoRemoto__FilaExecucaoUrl"] == var.execucao_entrada_queue_url &&
      aws_ssm_parameter.api_fluxo_distribuido[0].value == "true" &&
      aws_ssm_parameter.api_aplicacao_status[0].value == "implantacao-pendente" &&
      aws_ssm_parameter.api_aplicacao_status[0].name == "/oficina-mecanica/development/status/api-aplicacao" &&
      kubernetes_deployment_v1.oficina_mecanica_api[0].spec[0].template[0].metadata[0].annotations["checksum/config"] == sha256(jsonencode(kubernetes_config_map_v1.oficina_mecanica_api[0].data))
    )
    error_message = "Modo, workers, checksum e disponibilidade devem refletir a configuração aplicada."
  }
}

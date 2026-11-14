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

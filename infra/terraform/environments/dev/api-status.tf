resource "aws_ssm_parameter" "api_aplicacao_status" {
  count       = var.api_deploy_enabled ? 1 : 0
  name        = "${local.ssm_base_path}/status/api-aplicacao"
  description = "Disponibilidade da API; ready somente após rollout e health pela esteira."
  type        = "String"
  value       = "implantacao-pendente"
  tags        = local.common_tags
}

resource "aws_ssm_parameter" "api_fluxo_distribuido" {
  count       = var.api_deploy_enabled ? 1 : 0
  name        = "${local.ssm_base_path}/api/fluxo_distribuido_habilitado"
  description = "Modo configurado do workload; consumir junto do status ready da API."
  type        = "String"
  value       = tostring(var.fluxo_distribuido_habilitado)
  tags        = local.common_tags
  depends_on  = [kubernetes_config_map_v1.oficina_mecanica_api]
}

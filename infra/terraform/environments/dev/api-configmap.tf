resource "kubernetes_config_map_v1" "oficina_mecanica_api" {
  count = var.api_deploy_enabled ? 1 : 0

  metadata {
    name      = "oficina-mecanica-api-config"
    namespace = kubernetes_namespace_v1.oficina_mecanica[0].metadata[0].name
  }

  data = {
    ASPNETCORE_ENVIRONMENT                         = "Staging"
    ASPNETCORE_URLS                                = "http://+:8080"
    Integracoes__FluxoDistribuido__Habilitado      = tostring(var.fluxo_distribuido_habilitado)
    ProcessamentoRemoto__Habilitado                = tostring(var.processamento_remoto_habilitado)
    ProcessamentoRemoto__ContaEsperada             = var.conta_aws
    ProcessamentoRemoto__Regiao                    = var.aws_region
    ProcessamentoRemoto__FilaEntradaUrl            = aws_sqs_queue.os_entrada.url
    ProcessamentoRemoto__FilaExecucaoUrl           = var.execucao_entrada_queue_url
    ProcessamentoRemoto__FilaOrcamentoPagamentoUrl = var.orcamento_pagamento_entrada_queue_url
    Database__ApplyMigrationsOnStartup             = "true"
    Database__SeedDemoData                         = "true"
    Integracoes__ClientesInternos__Habilitado      = tostring(var.clientes_internos_habilitado)
    DD_ENV                                         = "development"
    DD_LOGS_INJECTION                              = "true"
    DD_SERVICE                                     = "oficina-mecanica-api"
    DD_VERSION                                     = local.api_version
  }
}

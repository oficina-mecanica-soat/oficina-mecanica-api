variable "aws_region" {
  description = "AWS region where the infrastructure will be provisioned."
  type        = string
}

variable "ssm_environment_name" {
  description = "Nome do ambiente usado nos paths do SSM."
  type        = string
  default     = "development"
  validation {
    condition     = contains(["development", "homologation", "production"], var.ssm_environment_name)
    error_message = "Use um nome de ambiente documentado no Git Flow."
  }
}

variable "db_connection_string" {
  description = "Connection string SQL Server da API montada pela esteira a partir do RDS compartilhado."
  type        = string
  sensitive   = true
}

variable "api_deploy_enabled" {
  description = "Habilita o deploy da API no EKS gerenciado pelo Terraform."
  type        = bool
  default     = false
}

variable "api_image_uri" {
  description = "Imagem Docker completa da API publicada no ECR."
  type        = string
  default     = ""
}

variable "jwt_secret" {
  description = "Chave JWT usada pela API."
  type        = string
  sensitive   = true
  default     = ""
}

variable "clientes_internos_habilitado" {
  description = "Ativa a consulta privada de clientes somente na janela coordenada com a Auth Lambda."
  type        = bool
  default     = false
}

variable "clientes_internos_token" {
  description = "Credencial técnica exclusiva da consulta privada; não reutilizar JWT ou token de orçamento."
  type        = string
  sensitive   = true
  default     = ""
  validation {
    condition     = !var.clientes_internos_habilitado || can(regex("^[!-~]{32,256}$", var.clientes_internos_token))
    error_message = "Configure uma credencial técnica ASCII de 32 a 256 caracteres para habilitar a consulta privada."
  }
}

variable "webhook_token" {
  description = "Token do webhook de orcamento."
  type        = string
  sensitive   = true
  default     = ""
}

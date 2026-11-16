data "aws_ssm_parameter" "kubernetes_status" {
  name = "${local.ssm_base_path}/status/kubernetes"
}

data "aws_ssm_parameter" "kubernetes_cluster_name" {
  name = "${local.ssm_base_path}/kubernetes/cluster_name"
}

data "aws_ssm_parameter" "kubernetes_ecr_repository_name" {
  name = "${local.ssm_base_path}/kubernetes/ecr_repository_name"
}

data "aws_ssm_parameter" "kubernetes_ecr_repository_url" {
  name = "${local.ssm_base_path}/kubernetes/ecr_repository_url"
}

data "aws_ssm_parameter" "kubernetes_api_internal_node_port" {
  name = "${local.ssm_base_path}/kubernetes/api_internal_node_port"
}

data "aws_ssm_parameter" "rds_status" {
  name = "${local.ssm_base_path}/status/rds"
}

data "aws_ssm_parameter" "rds_endpoint" {
  name = "${local.ssm_base_path}/rds/endpoint"
}

data "aws_ssm_parameter" "rds_os_secret_arn" {
  name = "${local.ssm_base_path}/rds/os/secret_arn"
}

data "aws_ssm_parameter" "rds_os_status" {
  name = "${local.ssm_base_path}/status/rds-os"
}

resource "terraform_data" "kubernetes_ready" {
  lifecycle {
    precondition {
      condition     = data.aws_ssm_parameter.kubernetes_status.value == "ready" && trimspace(data.aws_ssm_parameter.kubernetes_cluster_name.value) != "" && trimspace(data.aws_ssm_parameter.kubernetes_ecr_repository_name.value) != "" && trimspace(data.aws_ssm_parameter.kubernetes_ecr_repository_url.value) != ""
      error_message = "Kubernetes/ECR compartilhado não está ready ou publicou um contrato SSM incompleto."
    }
  }
}

resource "terraform_data" "rds_ready" {
  lifecycle {
    precondition {
      condition     = data.aws_ssm_parameter.rds_status.value == "ready" && data.aws_ssm_parameter.rds_os_status.value == "ready" && trimspace(data.aws_ssm_parameter.rds_endpoint.value) != "" && trimspace(data.aws_ssm_parameter.rds_os_secret_arn.value) != ""
      error_message = "RDS ou banco exclusivo de OS não estão ready ou publicaram contrato incompleto."
    }
  }
}

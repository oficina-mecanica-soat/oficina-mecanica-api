# 🛡️ Guardrails AWS Academy

## Autorização e execução

Toda criação, alteração ou destruição de recursos reais ocorre exclusivamente pela esteira oficial do owner. A autorização da janela precede o merge que dispara o CD; merge, flag funcional ou CI aprovada não autorizam uma janela por conta própria. Não executar apply/destroy local, publicação manual de imagem ou aplicação manual de manifests.

O laboratório tem orçamento limitado. Planejar o encerramento pela esteira antes de iniciar a janela, preservando dados e evidências necessários. Fim de sessão e reset do Academy não substituem destroy verificado: o state pode sobreviver a recursos removidos pelo laboratório.

## Antes da janela

1. Conferir laboratório ativo, conta, região e validade das credenciais temporárias.
2. Configurar os três secrets AWS nos Environments dos oito repositórios, sem versionar valores ou copiá-los para Pods.
3. Conferir roles existentes do EKS, JWT compartilhado, token técnico privado, Datadog e configuração de pagamento nos respectivos owners. RDS gerencia sua senha administrativa; o bootstrap prepara a credencial exclusiva de Orçamento/Pagamento. Não existe setup por `TF_VAR_db_password` local.
4. Conferir `AWS_DEPLOY_ENABLED`, `AUTO_PR_ENABLED` e o controle versionado `TERRAFORM_ACTION` conforme a autorização vigente. Esses controles têm finalidades distintas.
5. Seguir a ordem única de [deploy AWS](deploy-aws.md), com dependências e contratos SSM validados. A API usa Service NodePort e NLB interno de ownership Kubernetes; não criar Load Balancer público no serviço.
6. Revisar o plano salvo da esteira, os guardrails e os checks antes de aplicar. Erros de acesso, credencial ou rede não provam ausência de recurso.

Validações locais sem aplicação continuam permitidas:

```powershell
terraform fmt -check -recursive infra/terraform
terraform -chdir=infra/terraform/environments/dev init -backend=false -input=false
terraform -chdir=infra/terraform/environments/dev validate
```

## Custos e encerramento

Conferir EKS/nodes, NAT Gateway, RDS SQL Server, NLB, ECR, armazenamento, filas, tabelas, secrets e logs, sem presumir custo zero por ausência de tráfego. O serviço responsável mantém o ownership de seus recursos.

Executar o procedimento de encerramento em [deploy AWS](deploy-aws.md), pela ordem inversa das dependências e com quiescência da Saga/outboxes/filas antes do destroy. Não fazer purge, excluir caches ou alterar state como atalho. Inventário vazio permite somente a recuperação especificamente prevista e autorizada pelos guardrails; não libera adoção de recursos desconhecidos.

Depois do destroy, confirmar por consultas read-only ausência dos recursos, limpeza dos contratos/status SSM e state sem recursos gerenciados. Falha de consulta não equivale a limpeza concluída. Não restaurar `apply` apenas para deixar o controle bonito: essa mudança pode iniciar novo provisionamento.

Consultas iniciais, sem equivaler a inventário completo:

```powershell
aws eks list-clusters --region us-east-1
aws rds describe-db-instances --region us-east-1
aws elbv2 describe-load-balancers --region us-east-1
aws ec2 describe-nat-gateways --region us-east-1
```

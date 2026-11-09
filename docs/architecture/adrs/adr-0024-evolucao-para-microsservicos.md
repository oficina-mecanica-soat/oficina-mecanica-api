# ADR-0024 — Evolução para três microsserviços

## Status

Aceita como direção de implementação em 09/11/2026, conforme decisões da equipe nesta preparação. Implementação e evidências ainda pendentes.

## Contexto e problema

A Fase 4 exige ao menos três serviços independentes com bancos próprios. O monólito contém Atendimento, Administrativo, Gestão de Estoque e Gestão de Ordem de Serviço. Abrir OS e recusar orçamento atualmente coordenam estoque e OS por transação local.

## Decisão

Evoluir por extração, preservando Clean Architecture e testes. A ADR-0001 permanece como histórico; esta decisão substitui a implantação única como arquitetura alvo.

| Repositório | Responsabilidade e dados exclusivos |
| --- | --- |
| oficina-mecanica-api | Serviço de OS: clientes, veículos, autenticação interna, cadastro administrativo e catálogo, OS e coordenação da Saga. SQL Server existente. Continua porta de entrada da documentação. |
| oficina-mecanica-orcamento-pagamento | Orçamentos imutáveis por versão, decisões do cliente, cobranças e referências Mercado Pago. Banco SQL Server próprio, sem tabelas compartilhadas com OS. |
| oficina-mecanica-execucao | Diagnóstico, reserva e movimentação de estoque, fila e execução de serviços. DynamoDB próprio. Recebe snapshots de itens e serviços, sem consultar bancos alheios. |

O catálogo permanece no serviço de OS; estoque físico pertence à Execução. Preços e descrições usados num orçamento são snapshots versionados. A existência do cliente e do veículo é validada por OS. Nenhuma entidade EF, projeto Domain ou migration será compartilhado entre serviços.

A Lambda de autenticação deixará de consultar o RDS diretamente: consultará endpoint interno autenticado de OS para verificar cliente ativo. Preservar resposta pública, claims e proteção contra enumeração do RFC-0001. Esta mudança substitui apenas o acesso direto a dados descrito no RFC-0001/ADR-0018; o transporte e a identidade técnica precisam de teste na integração privada existente. Não expor consulta de documento ao público.

## Justificativa e consequências

Três serviços atendem ao mínimo sem criar um serviço para cada cadastro. Reutilizar a API reduz reescrita. Bancos separados permitem autonomia, mas exigem consistência eventual, contratos e reconciliação. A compatibilidade das rotas atuais será mantida onde possível; rotas extraídas serão encaminhadas no Gateway, sem duplicar ownership. Testes existentes devem acompanhar as regras extraídas.

## Referências

RFC-0007, ADR-0001, ADR-0023 e enunciado da Fase 4.


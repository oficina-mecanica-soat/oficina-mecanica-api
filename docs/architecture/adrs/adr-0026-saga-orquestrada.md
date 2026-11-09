# ADR-0026 — Saga orquestrada no serviço de OS

## Status

**Status:** ✅ Aceito para implementação
**Data:** 09/11/2026
**Autores:** Gabriel de Sousa Silva, Geovanna Monteiro Scabio

A decisão define a arquitetura alvo; implementação, provisionamento e evidências permanecem sujeitos às tarefas e validações do backlog.

---

## 1. Contexto e Problema

A transação local OS/estoque não atravessa bancos independentes. É necessário coordenar o fluxo, tratar duplicatas, falhas e compensações e demonstrar a recuperação.

## 2. Fatores Decisivos (Drivers)

- Coordenar transações distribuídas sem acesso cruzado a bancos.
- Tornar o progresso e a recuperação observáveis.
- Preservar os estados públicos existentes da OS.
- Distinguir falhas técnicas de decisões de negócio e operações físicas irreversíveis.

## 3. Decisão Proposta

OS coordena uma máquina de estados persistida, com outbox e deduplicação. A Saga cobre preparação da execução (diagnóstico/reserva), orçamento, decisão do cliente e liberação para execução; após finalização, Orçamento/Pagamento registra cobrança Mercado Pago e pagamento confirmado libera a entrega.

Preservar os valores do enum StatusOrdemServico existente. Estados de integração e pagamento pertencem a registros separados; Finalizada não significa paga. Entrega exige pagamento confirmado. Recusa de orçamento cancela preparação e libera reservas; falha antes de iniciar execução também compensa reservas e orçamento. Falhas técnicas transitórias provocam repetição, não cancelamento imediato.

Reparo executado não é reversível: após início físico, falhas técnicas ficam pendentes de recuperação e não desfazem estoque ou serviço ficticiamente. Estorno financeiro exige operação do provedor confirmada; nunca marcar devolução apenas porque a mensagem falhou.

## 4. Justificativa

A orquestração centraliza as transições em OS, facilitando diagnóstico e demonstração de um fluxo distribuído com poucos serviços. A máquina de estados persistida permite retomar o trabalho após reinícios; compensações explícitas evitam tratar cancelamento como simples exclusão de dados.

## 5. Consequências (Trade-offs)

### ✅ Positivo (Ganhos)

- Fluxo e responsabilidade de coordenação explícitos.
- Compensações rastreáveis e recuperação após interrupção.
- Preservação dos estados públicos da OS.

### ❌ Negativo (Perdas/Riscos)

- O coordenador exige persistência, controle de versão e testes de concorrência.
- Consistência eventual torna alguns resultados assíncronos.
- Reparo físico e pagamento não podem ser desfeitos por rollback de banco; exigem recuperação ou compensação real.

Há mais estados e mensagens, mas um coordenador permite acompanhar recuperação e demonstrar a Saga. Aguardar aprovação/pagamento é espera de negócio, sem timeout técnico curto. Compensações precisam ser idempotentes e observáveis.

## 6. Referências

RFC-0007 e ADR-0024/ADR-0025.

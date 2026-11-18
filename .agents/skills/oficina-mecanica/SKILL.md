---
name: oficina-mecanica
description: Conduz implementação, revisão e preparação de PRs do projeto Oficina Mecânica FIAP, incluindo extração de código, contratos entre serviços e CI/CD. Use ao trabalhar nos repositórios oficina-mecanica-*; não se aplica a projetos sem esse contexto.
---

# Fluxo de trabalho da Oficina Mecânica

Este é o procedimento canônico do projeto. As regras obrigatórias estão em `AGENTS.md`; READMEs, ADRs/RFCs e backlog mantêm as decisões e o estado atual. Esta skill não autoriza escopo adicional, deploy ou merge.

## 1. Confirmar contexto e fontes

- Identificar repositório, remoto, branch, alterações locais e item do backlog. Preservar trabalho existente e conferir o estado atual do PR antes de tratar aprovação como merge concluído.
- Ler o `AGENTS.md` aplicável e o README do componente. Na API, consultar somente as ADRs/RFCs relevantes em `docs/architecture` e o plano em `docs/projeto/backlog-tecnico.md`. Em outro clone, localizar a API irmã; se ausente, consultar os arquivos atuais no GitHub. Se a fonte necessária não estiver acessível, explicitar a lacuna e perguntar, sem inventar contratos.
- Para critérios de entrega ou extração de domínio, consultar também o acervo existente em `../contexto-projeto/README.md` e suas fontes textuais quando disponível. Não copiar o acervo para o repositório nem tratar snapshots do ZIP como estado atual; apontar a lacuna se o material necessário estiver ausente.
- Se a sessão começou fora do repositório, ler essas instruções explicitamente: mudar o diretório de um comando não comprova descoberta automática. No primeiro levantamento, indicar brevemente as fontes e a referência equivalente usadas; não repetir documentos inteiros.

## 2. Delimitar a mudança antes de editar

Relacionar comportamento solicitado, owner, produtores/consumidores e arquivos previstos. Identificar código, regras, testes e dados existentes a reaproveitar. Para extração, preservar linguagem de domínio e invariantes, sem compartilhar banco, Domain ou migrations entre serviços.

Escolher a implementação de referência vigente e comparar sua estrutura com as instruções locais. A presença de uma pasta ou solução em outro repositório não autoriza reproduzi-la aqui. Aplicar as decisões já aprovadas; alinhar mudança de contrato/ownership ou estrutura auxiliar antes de implementá-la. Autorização de tarefa não transfere automaticamente trabalho entre Gabriel e Geovanna.

## 3. Implementar dentro do padrão

Manter as camadas e a organização por funcionalidade existentes, com Request/Response/Validator/UseCase quando aplicável. Adaptar somente as responsabilidades do componente. Em CI/CD, comparar workflows reais de API/VPC/Kubernetes, preservando Git Flow, checks obrigatórios, gates, plano salvo, state por conta/ambiente, contratos SSM e guardrails de destroy.

Não criar pastas de scripts, estrutura auxiliar alternativa ou scaffold para contornar a proibição. Se uma ferramenta propuser esses arquivos, ajustar sua saída antes de publicar. Validações operacionais permanecem nos YAMLs; temporários ficam fora do conteúdo versionado. Não criar novo agente, instalar outra skill ou alterar configuração local como efeito indireto de uma implementação.

## 4. Validar o comportamento e os limites

Executar as verificações pertinentes do README e do CI; manter os gates existentes. Reaproveitar testes que protegem as regras extraídas e acrescentar cenários de falha relevantes. Diferenciar testes locais/mocks de prova real de IAM, FIFO/DLQ, rede e execução no workload.

Aprovação/merge de PR não é autorização para subir AWS. Manter o bloqueio de deploy enquanto a janela não estiver autorizada; avisar Gabriel quando a validação exigir novo laboratório/environments. Nunca substituir falha de acesso por ausência de recurso ou compensação bem-sucedida.

## 5. Revisar o diff antes de publicar

Atualizar `docs/projeto/backlog-tecnico.md` da API em cada entrega que altere progresso, dependência ou critério de aceite. Registrar o que foi implementado, o nível de validação realmente observado, os PRs/evidências e o que permanece pendente. Distinguir PR aberto de merge concluído, código integrado de implantação e testes isolados de aceite distribuído. Quando a implementação ocorrer em outro repositório, incluir a atualização do backlog canônico em PR documental associado, sem manter listas concorrentes. Após o merge informado por Gabriel, confirmar o estado e reconciliar o registro na próxima entrega. Não marcar trabalho local de outra frente como concluído sem publicação e verificação.

Não versionar arquivos de conversas, alinhamentos ou planos particulares nos serviços. Progresso e pendências ficam somente no backlog canônico. Não incluir acompanhamento de backlog nos READMEs; suas conclusões finais serão atualizadas ao encerrar as tarefas. Não alterar o README da API nesta frente.

Conferir `git status --short`, diff completo e `git diff --check`. Revisar também `git diff --cached --name-status` antes do commit e o diff da branch contra a base do PR antes do push.

Para cada arquivo adicionado, verificar diretórios introduzidos, necessidade e aderência à estrutura aprovada; procurar pastas proibidas, saídas de geradores, temporários e segredos. Confirmar que não há renomes de componentes, alteração de contratos ou enfraquecimento de gates fora do escopo. Executar o gate de estrutura definido em `ci.yml`; uma revisão textual não o substitui. Se surgir nova decisão necessária, fazer o trabalho independente e perguntar antes da parte dependente.

## 6. Entregar um PR concreto

Com os ajustes autorizados prontos e verificáveis, abrir o PR sem pedir novamente uma autorização já concedida. Informar tarefa e comportamento entregue, código reaproveitado/origem, componentes/contratos afetados, recursos previstos no futuro apply, esteiras acionadas, dependências, testes/evidências e limites. Deixar claro se há apenas integração de código ou implantação real.

Acompanhar o CI e corrigir falhas no escopo. Não declarar check aprovado sem observar seu resultado nem tarefa completa com validação pendente. Enviar link e resumo para Gabriel revisar e fazer o merge; não executar merge por ele. Não atualizar os arquivos de orientação para declarar uma decisão ainda não aprovada como regra do projeto.

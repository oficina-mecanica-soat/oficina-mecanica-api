const fs = require('node:fs');
const path = require('node:path');
const cp = require('node:child_process');

function contratosDoState(state) {
  const atributos = (tipo, nome) => (state.resources || []).find(r => r.mode === 'managed' && !r.module && r.type === tipo && r.name === nome)?.instances?.[0]?.attributes || {};
  const entrada = atributos('aws_sqs_queue', 'os_entrada');
  const dlq = atributos('aws_sqs_queue', 'os_dlq');
  return {
    entrada_queue_url: entrada.url || entrada.id || '', entrada_queue_arn: entrada.arn || '',
    dlq_url: dlq.url || dlq.id || '', dlq_arn: dlq.arn || ''
  };
}

const atributosContagem = ['ApproximateNumberOfMessages', 'ApproximateNumberOfMessagesNotVisible', 'ApproximateNumberOfMessagesDelayed'];
function verificarPendencias(contratos, consultar) {
  for (const url of [contratos.entrada_queue_url, contratos.dlq_url].filter(Boolean)) {
    const resposta = consultar('sqs', 'get-queue-attributes', '--queue-url', url, '--attribute-names', ...atributosContagem);
    if (resposta === null) continue;
    for (const atributo of atributosContagem) {
      const valor = resposta.Attributes?.[atributo];
      if (typeof valor !== 'string' || !/^\d+$/.test(valor) || Number(valor) !== 0)
        throw new Error('Mensagens pendentes ou contagem inválida na fila/DLQ: destroy bloqueado.');
    }
  }
}

function verificarInfra(contratos, prefix, consultar) {
  if (Object.values(contratos).some(valor => !valor)) throw new Error('State incompleto: contratos de dados/mensageria ausentes.');
  for (const [url, arn, dlq] of [[contratos.entrada_queue_url, contratos.entrada_queue_arn, false], [contratos.dlq_url, contratos.dlq_arn, true]]) {
    const atributos = consultar('sqs', 'get-queue-attributes', '--queue-url', url, '--attribute-names', 'All')?.Attributes;
    if (!atributos || atributos.QueueArn !== arn || atributos.FifoQueue !== 'true'
      || atributos.ContentBasedDeduplication !== 'false' || atributos.MessageRetentionPeriod !== (dlq ? '1209600' : '345600'))
      throw new Error('Fila real não corresponde ao contrato FIFO/retenção.');
    if (!dlq) {
      const redrive = JSON.parse(atributos.RedrivePolicy || '{}');
      if (atributos.VisibilityTimeout !== '60' || atributos.ReceiveMessageWaitTimeSeconds !== '20'
        || Number(redrive.maxReceiveCount) !== 5 || redrive.deadLetterTargetArn !== contratos.dlq_arn)
        throw new Error('Fila real não corresponde à recuperação definida na RFC.');
    }
  }
  for (const [nome, valor] of Object.entries(contratos)) {
    const parametro = consultar('ssm', 'get-parameter', '--name', `${prefix}/api/${nome}`)?.Parameter;
    if (parametro?.Value !== valor || parametro.Type !== 'String') throw new Error('Contrato SSM divergente do state.');
  }
  if (consultar('ssm', 'get-parameter', '--name', `${prefix}/status/api`)?.Parameter?.Value !== 'ready')
    throw new Error('Status de mensageria de OS não está ready.');
}

function verificarRemocao(contratos, prefix, consultar) {
  const confirmar = (...args) => { if (consultar(...args) !== null) throw new Error('Recurso ainda existe após destroy.'); };
  for (const url of [contratos.entrada_queue_url, contratos.dlq_url].filter(Boolean)) confirmar('sqs', 'get-queue-attributes', '--queue-url', url, '--attribute-names', 'QueueArn');
  for (const nome of Object.keys(contratos)) confirmar('ssm', 'get-parameter', '--name', `${prefix}/api/${nome}`);
  confirmar('ssm', 'get-parameter', '--name', `${prefix}/status/api`);
}

function consultarAws(...args) {
  try {
    const resposta = JSON.parse(cp.execFileSync('aws', [...args, '--region', process.env.AWS_REGION || 'us-east-1', '--output', 'json'], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }));
    if (resposta === null || typeof resposta !== 'object' || Array.isArray(resposta)) throw new Error('Resposta AWS inválida.');
    return resposta;
  } catch (error) {
    const ausencias = { sqs: ['(AWS.SimpleQueueService.NonExistentQueue)', '(QueueDoesNotExist)'], ssm: ['(ParameterNotFound)'] };
    if (ausencias[args[0]]?.some(codigo => String(error.stderr).includes(codigo))) return null;
    throw new Error(`Consulta ${args[0]}/${args[1]} falhou sem comprovar ausência; operação bloqueada.`);
  }
}
function main() {
  const dir = process.env.TF_WORKING_DIRECTORY;
  const contexto = path.join(dir, 'os-mensageria-destroy-context.json');
  const stateFile = path.join(dir, 'terraform.tfstate');
  const contratos = contratosDoState(fs.existsSync(stateFile) ? JSON.parse(fs.readFileSync(stateFile, 'utf8')) : {});
  const prefix = `/oficina-mecanica/${process.env.SSM_ENVIRONMENT_NAME}`;
  switch (process.argv[2]) {
    case 'before-destroy':
      verificarPendencias(contratos, consultarAws);
      fs.writeFileSync(contexto, JSON.stringify(contratos));
      console.log('Nenhuma mensagem observada em filas/DLQ; produtores devem permanecer parados. Outbox SQL não é verificada por este guardrail.');
      break;
    case 'after-destroy':
      verificarRemocao(JSON.parse(fs.readFileSync(contexto, 'utf8')), prefix, consultarAws);
      console.log('Filas e contratos de mensageria de OS comprovadamente ausentes.');
      break;
    case 'after-apply':
      verificarInfra(contratos, prefix, consultarAws);
      console.log('Recursos e contratos de infraestrutura conferidos; não comprova acesso do workload.');
      break;
    default: throw new Error('Modo de verificação inválido.');
  }
}
module.exports = { contratosDoState, verificarPendencias, verificarInfra, verificarRemocao, consultarAws };
if (require.main === module) { try { main(); } catch (error) { console.error(error.message); process.exit(1); } }

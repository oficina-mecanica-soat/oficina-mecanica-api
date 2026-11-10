const { test } = require('node:test');
const assert = require('node:assert/strict');
const { contratosDoState, verificarPendencias, verificarInfra, verificarRemocao, consultarAws } = require('./verificar-mensageria-os.cjs');
const contratos = { entrada_queue_url: 'entrada', entrada_queue_arn: 'arn:entrada', dlq_url: 'dlq', dlq_arn: 'arn:dlq' };
const vazia = { Attributes: { ApproximateNumberOfMessages: '0', ApproximateNumberOfMessagesNotVisible: '0', ApproximateNumberOfMessagesDelayed: '0' } };

test('destroy de OS bloqueia mensagens visíveis, em voo, atrasadas e em DLQ', () => {
  verificarPendencias(contratos, servico => { assert.equal(servico, 'sqs'); return vazia; });
  for (const atributo of Object.keys(vazia.Attributes)) {
    for (const fila of ['entrada', 'dlq']) {
      assert.throws(() => verificarPendencias(contratos, (servico, operacao, ...args) =>
        args.includes(fila) ? { Attributes: { ...vazia.Attributes, [atributo]: '1' } } : vazia), /Mensagens pendentes/);
    }
  }
  assert.throws(() => verificarPendencias(contratos, () => ({ Attributes: {} })), /contagem inválida/);
});
test('ausência confirmada permite recuperação; resposta existente ou falha real bloqueia', () => {
  verificarPendencias(contratos, () => null);
  verificarRemocao(contratos, '/oficina-mecanica/development', () => null);
  assert.throws(() => verificarPendencias(contratos, () => { throw new Error('AccessDenied'); }), /AccessDenied/);
  assert.throws(() => verificarRemocao(contratos, '/oficina-mecanica/development', () => { throw new Error('Credencial expirada'); }), /Credencial expirada/);
  assert.throws(() => verificarRemocao(contratos, '/oficina-mecanica/development', () => ({})), /ainda existe/);
});
test('contexto vem dos recursos de OS no state mesmo sem outputs após falha parcial', () => {
  const recurso = (name, attributes) => ({ mode: 'managed', type: 'aws_sqs_queue', name, instances: [{ attributes }] });
  assert.deepEqual(contratosDoState({ resources: [recurso('os_entrada', { id: 'entrada', arn: 'arn:entrada' }), recurso('os_dlq', { url: 'dlq', arn: 'arn:dlq' }), recurso('outro', { id: 'nao-pertence-a-este-contrato' })] }), contratos);
  assert.ok(Object.values(contratosDoState({})).every(valor => valor === ''));
});
test('pós-apply compara FIFO, retenção, redrive e contratos do owner sem consultar bancos', () => {
  const prefix = '/oficina-mecanica/development';
  const consultar = (servico, operacao, ...args) => {
    assert.ok(['sqs', 'ssm'].includes(servico));
    if (servico === 'sqs') return { Attributes: { QueueArn: args.includes('entrada') ? 'arn:entrada' : 'arn:dlq', FifoQueue: 'true', ContentBasedDeduplication: 'false', MessageRetentionPeriod: args.includes('entrada') ? '345600' : '1209600', VisibilityTimeout: '60', ReceiveMessageWaitTimeSeconds: '20', RedrivePolicy: JSON.stringify({ maxReceiveCount: '5', deadLetterTargetArn: 'arn:dlq' }) } };
    const nome = args[args.indexOf('--name') + 1];
    if (nome === `${prefix}/status/api`) return { Parameter: { Value: 'ready' } };
    const chave = nome.slice(`${prefix}/api/`.length);
    assert.ok(chave in contratos);
    return { Parameter: { Value: contratos[chave], Type: 'String' } };
  };
  verificarInfra(contratos, prefix, consultar);
  assert.throws(() => verificarInfra({ ...contratos, dlq_arn: '' }, prefix, consultar), /State incompleto/);
  assert.throws(() => verificarInfra(contratos, prefix, (servico, ...args) => servico === 'ssm' ? { Parameter: { Value: 'outro', Type: 'String' } } : consultar(servico, ...args)), /Contrato SSM/);
  assert.throws(() => verificarInfra(contratos, prefix, (servico, ...args) => servico === 'sqs' ? { Attributes: {} } : consultar(servico, ...args)), /Fila real/);
});
test('classificação da resposta AWS não converte AccessDenied, rede ou JSON inválido em ausência', () => {
  const cp = require('node:child_process');
  const original = cp.execFileSync;
  try {
    for (const codigo of ['AWS.SimpleQueueService.NonExistentQueue', 'QueueDoesNotExist']) {
      cp.execFileSync = () => { const erro = new Error('simulado'); erro.stderr = `An error occurred (${codigo})`; throw erro; };
      assert.equal(consultarAws('sqs', 'get-queue-attributes'), null);
    }
    for (const codigo of ['AccessDenied', 'ExpiredToken', 'RequestTimeout']) {
      cp.execFileSync = () => { const erro = new Error('simulado'); erro.stderr = `An error occurred (${codigo})`; throw erro; };
      assert.throws(() => consultarAws('sqs', 'get-queue-attributes'), /sem comprovar ausência/);
    }
    for (const resposta of ['json-invalido', 'null', '[]', '"texto"']) {
      cp.execFileSync = () => resposta;
      assert.throws(() => consultarAws('sqs', 'get-queue-attributes'), /sem comprovar ausência/);
    }
  } finally { cp.execFileSync = original; }
});

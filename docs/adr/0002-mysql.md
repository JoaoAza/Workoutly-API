# Decisão 0002 Preparação explícita do MySQL

Data: 20/10/2026. Estado: adotada para DB-01.

Usar MySqlConnector 2.6.2 e SQL explícito neste incremento pequeno, sem introduzir ORM ou modelos de domínio antes de implementar seus fluxos. Servidor local reproduzível via Docker Compose com MySQL 9.6.0, conta da API limitada ao banco do projeto e credenciais aleatórias no `.env` ignorado.

A API inicia sem banco para permitir diagnóstico. `/health` é liveness; `/health/ready` consulta a versão do esquema e responde 503 quando a dependência não está pronta. Não incluir detalhes de conexão nos retornos.

Aplicar a preparação somente pelo comando `--migrate`; não executar DDL automaticamente ao abrir a API. A primeira migração cria o histórico de versões e registra o bootstrap. DDL MySQL tem commit implícito, por isso o bootstrap é idempotente e reaplicável após interrupção. Migrações futuras serão adicionadas junto com as tabelas dos incrementos correspondentes; não modificar versões já aplicadas.

Testar o estado HTTP sem banco e a persistência em um MySQL real. Os testes administrativos usam base temporária com nome aleatório e removem somente essa base. Não usar SQLite ou mocks para afirmar compatibilidade MySQL. O app permanece sem driver ou credenciais de banco.

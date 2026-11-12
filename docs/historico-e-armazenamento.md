# Histórico, indicadores e armazenamento

HIST-01 entrega histórico por período, frequência, volume, evolução de carga e acompanhamento pelo professor. A versão do app é 0.10.0; o esquema MySQL continua em 7, pois as tabelas existentes já contêm os dados necessários. Não foi criada uma segunda cópia persistente dos indicadores.

## Como usar

- **Aluno:** no início, abra **Histórico e evolução**. O período inicial são os últimos 30 dias, incluindo hoje. Escolha De/Até e toque em **Consultar período**. Trocar as datas oculta os resultados anteriores até nova consulta.
- Veja quantidade de treinos, dias ativos, séries, repetições, duração total e volume. A frequência informa quantos treinos começaram em cada dia com registro.
- Em **Evolução de carga**, selecione um exercício realizado no período. O gráfico e os valores escritos mostram a maior carga por sessão, com séries, repetições e volume como contexto. Um único registro aparece como ponto, com aviso de que ainda não há comparação temporal.
- **Ver treino** abre as séries e observações da sessão original. O aluno pode tocar em **Corrigir treino**; voltar ao histórico faz nova consulta e recalcula os indicadores.
- **Professor/admin:** abra **Meus alunos → Histórico do aluno**. Os filtros e indicadores são os mesmos; os detalhes são somente de leitura. O vínculo ativo permite consultar também treinos anteriores ao vínculo. A confirmação do aluno explica esse acesso. Remover o vínculo preserva os registros e bloqueia novas consultas do professor.
- Sem conexão, as datas ficam preservadas e a tela oferece nova consulta. Os resultados anteriores ficam ocultos para evitar exibir dados desatualizados. Não há sincronização offline ou persistência local do histórico.

## O que os números significam

| Indicador | Regra |
| --- | --- |
| Treinos / frequência | Uma contagem por sessão cujo início está no período. Exercícios e séries não multiplicam a contagem. |
| Dias ativos | Quantidade de datas distintas com início de sessão, no fuso informado. Dois treinos no mesmo dia são dois treinos e um dia ativo. |
| Séries e repetições | Quantidades efetivamente registradas; metas da ficha não entram no cálculo. |
| Duração | Soma das durações atuais das sessões selecionadas. |
| Volume de carga | Soma de `repetições × carga em kg` de cada série realizada. Ex.: 10 × 42,5 + 6 × 45 + 12 × 25 = **995 kg**. É volume registrado, não massa corporal ou gasto energético. |
| Evolução por exercício | Maior carga efetivamente informada nas séries de cada sessão, em ordem cronológica. Não estima força máxima; a comparação exige considerar também as repetições e as condições do exercício. |

Carga zero é válida e participa da contagem, com volume zero. Datas sem treino não ganham sessões nem pontos artificiais. Exclusão lógica ou edição da ficha preserva o histórico, pois as sessões apontam para a revisão executada. O seletor usa o último nome executado no período; os detalhes de cada treino mantêm os nomes daquela revisão. Exercícios diferentes, mesmo com nomes iguais, permanecem separados por ID.

## Onde os dados ficam guardados

O caminho é **app MAUI → API ASP.NET Core → MySQL**. O app nunca conecta diretamente ao MySQL.

| Informação | Armazenamento |
| --- | --- |
| Conta, papel e vínculo | `users`, `account_roles`, `student_teachers`; o vínculo associa IDs, sem transferir a propriedade dos dados ao professor. |
| Acesso | Senha com hash pelo PasswordHasher; `user_sessions` guarda o hash do token revogável. No aparelho, SecureStorage guarda token e endereço do serviço; a senha não é persistida. |
| Exercícios e fichas | `exercises`, `workout_plans`, `workout_plan_versions`, `workout_plan_items`. Cada revisão guarda a descrição dos exercícios e as metas daquele planejamento. |
| Treino salvo | Uma linha em `workout_sessions`: ID, aluno, ficha/revisão, início em UTC, duração, observações e identificação da criação. |
| Séries realizadas | Linhas em `workout_session_sets`, ligadas ao treino e exercício, com posição, repetições e carga decimal. O exemplo de 995 kg tem três linhas de série e uma linha de sessão. |
| Identificação das correções | `workout_session_changes` registra versão, operação e hash para concorrência/reenvio. As séries atuais são atualizadas em transação. Essa tabela não permite restaurar todos os valores anteriores de uma correção. |
| Histórico e indicadores | Consultas às sessões e séries atuais; totais e pontos do gráfico são enviados pela API e ficam em memória no app enquanto a tela está aberta. Não há tabela de gráficos ou cache persistente de totais. |

Cada sessão tem um aluno proprietário. Para o próprio aluno, a API usa o ID obtido do token. Para o professor, exige papel `teacher` e procura a relação com aquele aluno no MySQL em cada requisição. A relação fica protegida durante a leitura; a remoção aguarda uma consulta já em andamento, e as próximas consultas ficam bloqueadas. Lista, totais e evolução usam uma transação de leitura consistente, para não misturar valores anteriores e posteriores a uma correção concorrente. Respostas autenticadas usam `Cache-Control: no-store`.

### Neste Mac

Ao abrir `Executar-local.command`, `scripts/dev-native.py` inicia uma instância exclusiva do MySQL, banco `workoutly`. Os arquivos físicos ficam em:

```text
/Users/pedrobaldi/Downloads/Sexto Semestre/Projeto Integrador/Workoutly/.local/native-mysql/data
```

São arquivos gerenciados pelo MySQL, não documentos para editar manualmente. **Fechar o app ou encerrar os serviços com Ctrl+C não apaga esses arquivos.** Ao iniciar novamente pelo helper, as contas e os treinos são recuperados. O socket de conexão é temporário, mas o diretório de dados é persistente. A configuração secreta fica em `.env`; nem ela nem `.local` entram no Git.

O **Git registra código, migrações, testes e documentação**. Um commit, tag ou clone não faz backup dos treinos. Apagar a pasta de dados perde o banco local; não existe backup automático nesta etapa. Para transferir os dados para outra máquina, deve-se fazer exportação/restauração pelo MySQL e configurar as credenciais separadamente. Não copiar arquivos de um banco em execução como forma de exportação.

Ainda não há servidor em nuvem: a API e o banco usados na demonstração estão neste Mac. Em uma implantação futura, o mesmo modelo ficará no MySQL do servidor configurado, com API HTTPS acessível aos aparelhos e rotina de backup definida nesse ambiente. Trocar de celular e entrar na mesma conta recupera os registros se ambos estiverem conectados à mesma API/banco; a hospedagem e esse teste em aparelhos físicos permanecem pendentes.

## Contratos HTTP

| Método e rota | Acesso |
| --- | --- |
| `GET /history?from=2026-10-01&to=2026-10-21&offsetMinutes=-180` | Aluno autenticado; somente os próprios registros. |
| `GET /teaching/students/{studentId}/history` com a mesma query | Professor com vínculo ativo com o aluno. Sem vínculo: 404. |
| `GET /teaching/students/{studentId}/sessions/{id}` | Detalhes de uma sessão do aluno vinculado; somente leitura. |

`from` e `to` usam `yyyy-MM-dd`; ambas as datas são incluídas. A API converte meia-noite de `from` e meia-noite do dia após `to` para UTC, usando `offsetMinutes`, e seleciona `início >= primeiro limite e < segundo limite`. Treino atravessando meia-noite pertence ao dia em que começou.

São aceitos até 366 dias, datas desde 01/01/2000, fim anterior a 31/12/9999 e offset entre −840 e +840 minutos. Datas ausentes/inválidas, ordem invertida ou período maior retornam 400. O app limita a data final a hoje e usa o deslocamento UTC atual do aparelho, exibido no filtro. Esse é um **fuso fixo para a consulta**, inclusive em períodos históricos; não aplica regras históricas de horário de verão. No ambiente validado, UTC−03:00.

Resposta: datas/fuso, `sessionCount`, `activeDays`, `setCount`, `repetitionCount`, `durationSeconds`, `volumeKg`, `days` (data e quantidade), `sessions` (resumos) e `exercises` (ID, nome e pontos). Cada ponto informa sessão, início UTC, maior carga, séries, repetições e volume. Período vazio retorna 200 com totais zero e listas vazias. Sem sessão: 401; papel incorreto: 403; recurso indisponível/fora do vínculo: 404; banco indisponível: 503 genérico. Não há rotas de escrita do histórico do professor.

## Conceito, código e verificação

- **Dados derivados:** `WorkoutHistoryService.cs` consulta as séries atuais e calcula os indicadores, evitando divergência entre um total armazenado e os dados corrigidos.
- **Autorização por relação:** `WorkoutHistoryEndpoints.cs` exige o perfil e o serviço valida o vínculo no MySQL. Ocultar o botão de edição no app melhora a interface; a proteção efetiva está na API.
- **Transação:** a leitura consistente mantém lista e indicadores da mesma versão dos registros durante a requisição.
- **Apresentação:** `WorkoutsPage.xaml/.cs` aplica o período e desenha o gráfico com `GraphicsView`, sem nova biblioteca. `AccountClient.cs` envia a requisição autenticada e desserializa os contratos com geração de código. `ConnectionsPage` abre o contexto do aluno e `WorkoutDetailsPage` usa a rota de professor quando apropriado.
- **Como testar:** `python3 scripts/test-native-mysql.py` executa a suíte HTTP com MySQL real temporário e isolado. `WorkoutHistoryTests.cs` verifica cálculos conhecidos, limites de data/fuso, correção, exclusão da ficha, reinício e isolamento entre aluno e dois professores. Ver resultados efetivos em [evidências](evidencias/historico.md).

Limites desta etapa: a consulta é por período e sem paginação; períodos muito movimentados podem exigir paginação no futuro. Android foi compilado, mas a navegação foi verificada somente no simulador de iPhone. Avaliação com três participantes reais e validação multiplataforma completa continuam pendentes.

# HIST-01 — histórico, indicadores e acompanhamento

Data: 21/10/2026. App 0.10.0 (10), tag `v0.10.0-historico`, branch `desenvolvimento/historico-indicadores`. Continuação da versão `v0.9.0-correcao-treinos`; documentos acadêmicos da pasta superior preservados.

## Ambiente

Mac Apple Silicon, .NET SDK 10.0.401, workload set 10.0.401.1, MAUI 10.0.110, MySQL 9.6.0 nativo. Interface executada no iPhone 18 Pro Simulator com iOS 27.0. Android compilado com JDK 21; sem execução Android nesta etapa. Esquema MySQL 7, sem migração adicional.

## Testes automatizados efetivamente executados

Comando na raiz: `python3 scripts/test-native-mysql.py`.

Resultado final: **30 aprovados, 0 falhas, 0 ignorados**. Instância temporária de MySQL por socket, sem TCP e isolada da base de demonstração. O script também observou HTTP 503 com esquema pendente, migração explícita, HTTP 200 pronto e HTTP 200 após reinício da API. A instância temporária foi encerrada e seus arquivos removidos.

Três testes novos em `WorkoutHistoryTests`:

1. **Cálculos, datas e persistência:** período 01/04/2025 a 03/04/2025 no UTC−03:00. Sessão um segundo antes da meia-noite inicial e sessão exatamente na meia-noite seguinte ao fim ficam fora. Três sessões incluídas, duas no mesmo dia: 2 dias ativos, 4 séries, 35 repetições, 1.800 segundos, volume 550 kg; evolução 25 → 30 → 0 kg, sem perder a sessão com carga zero. Após exclusão lógica da ficha e correção da primeira sessão: 3 sessões, 3 séries, 19 repetições, 2.100 segundos, volume 230 kg e primeiro ponto de 40 kg. Nova instância da API recuperou os mesmos 230 kg. Período vazio retornou totais zero e listas vazias.
2. **Autorização:** antes do vínculo, professor recebe 404. Após confirmação do aluno, consulta histórico e detalhes; outro professor recebe 404. Outro aluno não lê a sessão nem obtém indicadores alheios informando studentId na query. Aluno não usa rota de professor (403), professor não usa histórico pessoal de aluno (403), anônimo recebe 401. Escrita pelo professor continua bloqueada. Ao remover ou trocar o vínculo, o professor anterior perde o acesso; os dados próprios do aluno permanecem.
3. **Validação de período:** datas ausentes ou malformadas, ordem invertida, período maior que 366 dias, data anterior a 2000 e offset fora de ±840 retornam 400. Ano bissexto completo com 366 dias é aceito. A mesma sessão de 02:00 UTC pertence a 01/04 em UTC e a 31/03 em UTC−03:00.

O teste de cálculos primeiro falhou com 404, antes da rota existir; passou após implementar a leitura. O teste de professor também falhou com 404 após o vínculo, antes da rota de acompanhamento; passou com a autorização por relação. A skill local `tdd` orientou os testes pela interface HTTP, sem simular MySQL ou verificar métodos privados.

## Compilações

```sh
/Users/pedrobaldi/.dotnet/dotnet build src/Workoutly.Mobile/Workoutly.Mobile.csproj -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
/Users/pedrobaldi/.dotnet/dotnet build src/Workoutly.Mobile/Workoutly.Mobile.csproj -f net10.0-android -p:JavaSdkDirectory=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home
```

Ambas concluídas com **0 avisos e 0 erros**. A API e os testes também foram compilados durante a suíte de integração.

## Verificação manual no iPhone Simulator

Usadas somente as contas fictícias locais de demonstração, previamente criadas. Não foram inseridas sessões adicionais na base de demonstração para fabricar uma evolução.

| Ação | Resultado observado |
| --- | --- |
| Abrir Histórico e evolução como aluno | Período inicial 22/09/2026–21/10/2026, UTC−03:00. Recuperou o treino anterior: 1 sessão, 1 dia ativo, 3 séries, 28 repetições, 00:03:30 e 995 kg. |
| Abrir evolução do supino | Um ponto de 45 kg, 2 séries, 16 repetições e volume 695 kg; aviso explícito de único registro. |
| Trocar para tríceps | Atualizou para 25 kg, 1 série, 12 repetições e volume 300 kg. |
| Consultar até 20/10/2026 | Totais zero, sem gráfico e mensagem de ausência de treinos. |
| Encerrar somente os serviços locais e consultar novamente | Mensagem de falha de conexão, datas preservadas, resultados antigos ocultos. |
| Reiniciar pelo Executar-local.command e repetir | Consulta voltou a responder, mantendo as mesmas datas. `/health/ready` confirmou versão 7. |
| Entrar como professor → Meus alunos → Histórico do aluno | Recuperou o mesmo treino e os mesmos 995 kg após o reinício; contexto nomeado e somente de leitura. |
| Professor abrir Ver treino | Séries e observações corretas; botão Corrigir treino ausente. A API também bloqueou a escrita nos testes. |

Capturas:

- [Indicadores do aluno](ios-historico-indicadores.jpg)
- [Evolução e treino salvo](ios-historico-evolucao.jpg)
- [Período vazio](ios-historico-vazio.jpg)
- [Falha de conexão e datas preservadas](ios-historico-sem-conexao.jpg)
- [Indicadores pelo professor](ios-historico-professor.jpg)
- [Detalhes somente de leitura](ios-professor-treino-leitura.jpg)

## Sequência reproduzível

1. Abra `Executar-local.command` e aguarde a API na porta 5080. Não inicie outra instância em paralelo.
2. Execute o app no iPhone Simulator. Entre como aluno, abra Histórico e evolução e consulte datas que incluam os treinos salvos.
3. Compare o volume dos indicadores com as séries em Ver treino; selecione exercícios diferentes.
4. Escolha um período vazio; confirme zeros e ausência de pontos.
5. Como professor com vínculo ativo, abra Meus alunos → Histórico do aluno e confira a consulta sem edição.
6. Para verificar isolamento e cálculos com múltiplas sessões sem modificar dados da demonstração, execute a suíte MySQL temporária.

## Limites

Não foram executados Android em emulador/aparelho, Windows, iPhone físico, Release ou avaliação com participantes reais. O gráfico com múltiplos pontos foi validado nos dados retornados pela API; a captura do simulador mostra somente o registro de demonstração existente. Não há hospedagem, backup automático, paginação ou sincronização offline. O filtro usa offset fixo explícito, sem conversão histórica de horário de verão.

Contrato, conceitos e armazenamento físico estão em [Histórico e armazenamento](../historico-e-armazenamento.md). UX-01, QA-01, AVAL-01 e DOC-01 permanecem pendentes; estas evidências não substituem avaliação acadêmica com participantes.

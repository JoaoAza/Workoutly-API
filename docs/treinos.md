# Registro de treinos

TREINO-01 permite ao aluno executar uma ficha e salvar os dados realizados pela API no MySQL. Inclui consulta dos próprios registros para conferir o salvamento. A correção posterior foi implementada em TREINO-02 (correcao-treinos.md); indicadores por período e acompanhamento pelo professor foram implementados em HIST-01, descrito em historico-e-armazenamento.md.

## Como usar

1. Inicie `Executar-local.command`, entre como aluno e abra **Minhas fichas**.
2. Toque em **Iniciar treino**. O app consulta a ficha salva atual e abre sua execução; o cronômetro começa nessa tela.
3. Em cada série realizada, informe repetições e carga em kg e marque **Feita**. Os valores previstos aparecem como referência, sem preenchimento automático dos resultados.
4. Use **Adicionar série** ou **Remover última série** se necessário. Séries vazias e não marcadas são omitidas; se há campos preenchidos sem Feita, o app pede revisar antes de concluir. A remoção de uma série preenchida exige confirmação.
5. Informe observações opcionais e toque em **Concluir e salvar treino**. Confira a confirmação: somente séries marcadas serão registradas.
6. Após salvar, a tela apresenta data, duração, observações e cada série realizada. No início, **Histórico e evolução → Ver treino** recupera os registros pela API, inclusive após reabrir o aplicativo.

Cargas aceitam vírgula ou ponto decimal no app. Use **0 kg para peso corporal/sem carga externa**; não informe o peso do corpo como carga. A ficha não constitui prescrição clínica. Os valores limites do formulário são restrições de entrada, não recomendações de treino.

## Conexão e nova tentativa

Antes de iniciar, a API precisa estar disponível. Durante a execução, os campos ficam somente na página em memória; não há sincronização offline nem rascunho persistente depois de fechar o aplicativo.

Ao confirmar a conclusão, o app fixa os dados e a duração daquela tentativa. Se o envio falhar, mantém o formulário visível e protegido contra alterações e oferece **Tentar salvar novamente**. A espera pela conexão não aumenta a duração do treino concluído. **Conferir treinos salvos** permite verificar o resultado quando a resposta do serviço foi perdida, mantendo a página de envio na navegação.

Um erro de validação HTTP 400 libera os campos para revisão. Se o resultado do envio for incerto, mantenha a página aberta e repita a tentativa; não comece outro registro da mesma execução. A confirmação do botão Voltar avisa sobre a perda de dados não salvos. Encerrar o processo do app perde o formulário em memória.

## Conceitos e decisões

**Planejamento × execução.** A ficha define metas; o treino registra o que realmente aconteceu. O aluno pode realizar menos exercícios/séries que o previsto, ou até 20 séries por exercício. Não são criados resultados para séries deixadas vazias. Os registros armazenam repetições e carga por série, e duração/observações para a sessão inteira.

**Revisão executada.** A sessão referencia `(plan_id, plan_version)` da ficha aberta para execução. Nome, ordem e descrição dos exercícios vêm da revisão imutável, não do corpo enviado pelo cliente. Se o professor alterar a ficha ou ela for excluída enquanto o aluno treina, a execução pode terminar na revisão original. A autorização continua exigindo que a ficha pertença ao aluno autenticado; um ID alheio não concede acesso.

**Transação e integridade.** A migração `006_workout_sessions.sql` eleva o esquema à versão 6:

| Tabela | Responsabilidade |
| --- | --- |
| workout_sessions | Aluno, revisão executada, início em UTC, duração em segundos, observações e identificação da tentativa. |
| workout_session_sets | Exercício da revisão, posição da série realizada, repetições e carga decimal em kg. |

Chaves estrangeiras ligam cada série à sessão e ao exercício daquela revisão. A API salva cabeçalho e séries em uma transação: falhar a validação de um item não deixa uma sessão parcial. A carga usa DECIMAL no MySQL para preservar valores como 22,5 kg sem aproximação binária.

**Idempotência.** A página gera um UUID por execução. O servidor associa esse ID ao conteúdo normalizado da tentativa. O primeiro POST retorna 201; repetir ID/conteúdo retorna 200 com o registro existente. Alterar o conteúdo usando o mesmo ID retorna 409 e não sobrescreve dados. Horários equivalentes em fusos diferentes, casas decimais equivalentes e ordem dos exercícios no corpo não mudam a tentativa. A ordem das séries dentro do exercício é preservada. O hash SHA-256 aqui identifica o conteúdo; não substitui o PasswordHasher de senhas nem autentica usuários.

**Tempo.** O início usa o relógio UTC do aparelho; o cronômetro usa Stopwatch para medir a duração, inclusive o tempo decorrido em segundo plano enquanto a página continua viva. Ao concluir, a duração é fixada. A API aceita de 1 segundo a 24 horas, datas desde 2000 e no máximo cinco minutos de tolerância de relógio para início/término no futuro. O horário é normalizado em UTC com precisão de milissegundos e exibido no fuso local do aparelho.

**Privacidade.** `/sessions` exige o perfil student. O aluno é obtido da sessão de acesso e aplicado nas consultas SQL. Campos studentId e descrições adicionais enviados no corpo não mudam o proprietário nem os dados da revisão. Professor/admin consulta pela rota /teaching/students/{studentId}/sessions/{id}, com vínculo ativo e sem edição, conforme historico-e-armazenamento.md.

## Onde aparece no código

- `src/Workoutly.Api/Workouts/WorkoutEndpoints.cs`: valida entrada e aplica a política student aos contratos.
- `Workouts/WorkoutService.cs`: propriedade, transação, comparação da tentativa e leitura pela revisão original.
- `Data/Migrations/006_workout_sessions.sql`: persistência e restrições do banco.
- `src/Workoutly.Mobile/WorkoutPage.xaml` e `.xaml.cs`: cronômetro, séries, confirmação e reenvio preservado.
- `WorkoutsPage.xaml` e `WorkoutDetailsPage.xaml`, com code-behind: lista e recuperação dos registros salvos.
- `Services/AccountClient.cs`: contratos JSON e requisições autenticadas usando a sessão segura existente.

Os caminhos após cada projeto são relativos a ele. As telas reutilizam cores, Inter, campos e cartões locais; não foram inseridos valores fictícios de progresso nem indicadores antecipados.

## Contratos HTTP

| Rota | Resposta |
| --- | --- |
| POST /sessions | 201 criado; 200 para o mesmo ID/conteúdo já salvo. Location aponta para o detalhe. |
| GET /sessions | 200 com resumos próprios, do início mais recente para o mais antigo; array vazio se nenhum. |
| GET /sessions/{id} | 200 com detalhe próprio; 404 se inexistente ou de outra conta. |

Exemplo de corpo de criação (substitua os identificadores pelos UUIDs reais):

```json
{
  "id": "UUID da execução",
  "planId": "UUID da ficha própria",
  "planVersion": 2,
  "startedAt": "2026-10-21T12:00:00Z",
  "durationSeconds": 1500,
  "notes": "Observações desta execução",
  "exercises": [
    { "exerciseId": "UUID presente na revisão", "sets": [
      { "repetitions": 10, "weightKg": 22.5 },
      { "repetitions": 8, "weightKg": 25 }
    ] }
  ]
}
```

De 1 a 50 exercícios distintos da revisão, com 1 a 20 séries realizadas por exercício; repetições de 1 a 200; carga de 0 a 2000 kg com até duas casas decimais; observações até 1000 caracteres. Os totais previstos na ficha não limitam o número realizado dentro desses limites.

Resumo: id, planName, startedAt, durationSeconds, exerciseCount, setCount. Detalhe: id, studentId, planId, planVersion, planName, startedAt, durationSeconds, notes, exercises. Cada exercício traz exerciseId, name, muscleGroup, equipment, description, plannedSets, plannedRepetitions e sets com repetitions/weightKg. Os detalhes estão ordenados pela revisão e pela sequência realizada das séries.

Outros status: 400 inválido; 401 sem sessão de acesso; 403 perfil diferente; 404 revisão/ficha não encontrada ou alheia; 409 ID usado com outro conteúdo; 503 serviço indisponível. As respostas usam no-store e exigem HTTPS fora de Development. PUT /sessions/{id} foi adicionado em TREINO-02; veja correcao-treinos.md. DELETE de sessão permanece ausente.

O detalhe e o resumo agora incluem volumeKg calculado das séries atuais. O detalhe também retorna version do registro e planExercises da revisão original para a correção. Esses campos e seu uso estão documentados em correcao-treinos.md.

## Como verificar

Execute `python3 scripts/test-native-mysql.py` para os contratos públicos com MySQL real e isolado. `WorkoutSessionTests.cs` verifica criação/consulta, preservação após editar/excluir ficha, isolamento de contas, validações, reenvios equivalentes/concorrentes e rejeição de sobrescrita.

No simulador, faça o fluxo acima com dados fictícios, abra o detalhe salvo e reabra o app para recuperar pela lista. Para conferir a recuperação de conexão, mantenha o formulário aberto, encerre apenas o helper local, tente salvar, reinicie o ambiente e repita a mesma tentativa.

Veja resultados e limites em [evidências](evidencias/treinos.md). HIST-01 foi implementado posteriormente; filtros, indicadores e acompanhamento estão documentados em historico-e-armazenamento.md.

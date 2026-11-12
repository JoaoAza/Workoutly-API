# Correção de treinos salvos

TREINO-02 permite ao aluno corrigir seus próprios registros. Séries, repetições, cargas, duração e observações podem ser atualizadas; início, proprietário e revisão da ficha executada permanecem os mesmos. Indicadores por período e acompanhamento pelo professor foram implementados em HIST-01; ver historico-e-armazenamento.md.

## Como usar

1. No início do aluno, abra **Histórico e evolução → Ver treino → Corrigir treino**.
2. Ajuste horas, minutos e segundos da duração. Os campos vêm preenchidos com o registro atual.
3. Corrija as repetições e cargas de cada série. **Remover série** pede confirmação e retira o item somente do formulário, até salvar.
4. Use **Adicionar série** no exercício correspondente para registrar algo esquecido. Todos os exercícios da ficha original aparecem, inclusive os que ficaram sem séries na primeira gravação. Não é possível trocar o treino para outra ficha ou escolher movimentos que não pertencem à revisão executada.
5. Ajuste as observações e toque em **Salvar correção**, confirmando a atualização.
6. O detalhe e a lista passam a mostrar duração, séries, quantidade de exercícios e volume recalculados a partir dos dados salvos.

**Cancelar edição** ou o botão Voltar pede confirmação para descartar alterações não salvas. **Recarregar registro** também confirma a substituição dos campos e só a realiza quando a consulta ao serviço termina com sucesso.

Se o envio falhar, o formulário mantém os dados protegidos contra alterações e permite **Tentar salvar correção novamente**. Isso repete a mesma operação. Um erro de validação libera os campos para revisar. Se houver conflito de edição, os campos locais permanecem, e o aluno deve recarregar a versão atual antes de tentar corrigi-la. Não há rascunho persistente após fechar o app.

## Conceito e decisão

**Versão da ficha e versão do registro são diferentes.** planVersion identifica o planejamento executado e é imutável nesta operação. version identifica a edição atual do treino salvo, começa em 1 e aumenta a cada correção aceita. O app envia a versão que abriu; se outro aparelho já salvou uma correção, a API retorna 409 em vez de sobrescrever seus valores.

**Reenvio seguro.** Cada formulário de correção recebe um editId (UUID). Reenviar o mesmo editId, versão de base e conteúdo reconhece a operação já aplicada e retorna o registro atual, sem aumentar a versão novamente. Reutilizar editId com outro conteúdo retorna 409. Reenviar uma correção antiga que já foi aceita não desfaz uma correção posterior. A repetição do POST de criação também mantém os dados corrigidos.

**Atualização atômica.** A API verifica o aluno, a versão, a origem de cada exercício e os valores antes de atualizar. Cabeçalho, séries e identificação da correção são gravados na mesma transação. Uma falha não deixa duração nova com séries antigas nem altera outro usuário.

**Totais recalculados.** O volume corresponde à soma de repetições × carga em kg das séries registradas. A API o calcula a partir das séries atuais; não mantém uma cópia de total que possa ficar desatualizada. O detalhe recebe volumeKg; a lista recebe volumeKg, setCount e exerciseCount. Carga 0 contribui com volume 0; peso corporal não é convertido em carga externa. Esse total isolado não constitui avaliação clínica nem conclui o painel de evolução.

## Persistência e compatibilidade

A migração `007_workout_corrections.sql` eleva o esquema a **7** e adiciona workout_session_changes, com session_id, version, operation_id, request_hash e changed_at. Treinos anteriores recebem a versão inicial 1. Reaplicar a migração preserva versões já corrigidas.

A tabela identifica operações aceitas para concorrência e reenvio. Ela **não guarda cópia integral das séries antigas nem oferece restaurar correções anteriores**. As séries atuais continuam em workout_session_sets; o request_hash original de workout_sessions permanece para reconhecer a tentativa inicial de criação. As revisões de ficha continuam imutáveis e recuperáveis mesmo quando a ficha foi excluída.

## Onde aparece no código

- `src/Workoutly.Api/Workouts/WorkoutEndpoints.cs`: PUT e validações compartilhadas com o cadastro de sessões.
- `Workouts/WorkoutService.cs`: transação, propriedade, versão do registro, comparação da operação e cálculo dos totais.
- `Data/Migrations/007_workout_corrections.sql`: identificação de correções e adaptação dos registros anteriores.
- `src/Workoutly.Mobile/WorkoutEditPage.xaml` e `.xaml.cs`: formulário, séries, confirmação, recarregamento e nova tentativa.
- `WorkoutDetailsPage` abre a correção e atualiza seus dados ao retornar; `WorkoutsPage` apresenta os resumos recalculados.
- `Services/AccountClient.cs`: contrato UpdateWorkoutPayload e HTTP com a sessão segura existente.

Os caminhos após cada projeto são relativos a ele. A interface reutiliza os estilos, as cores e a fonte Inter já adotados no projeto.

## Contrato HTTP

`PUT /sessions/{id}` — somente student, e somente registro próprio:

```json
{
  "editId": "UUID desta correção",
  "version": 1,
  "durationSeconds": 1200,
  "notes": "Observações corrigidas",
  "exercises": [
    { "exerciseId": "UUID da ficha original", "sets": [
      { "repetitions": 8, "weightKg": 25 }
    ] }
  ]
}
```

O corpo representa todas as séries realizadas que devem permanecer. Exercícios omitidos deixam de ter séries no registro; não se altera o catálogo nem a ficha. É obrigatório manter pelo menos uma série. Valem os limites anteriores: até 50 exercícios distintos, 1 a 20 séries por exercício, 1 a 200 repetições, 0 a 2000 kg com até duas casas decimais, observações até 1000 caracteres e duração de 1 segundo a 24 horas. O término calculado a partir do início original não pode exceder cinco minutos à frente do relógio do servidor.

Resposta 200: detalhe atual com version, volumeKg e planExercises, além dos campos existentes. planExercises lista os movimentos e metas da revisão original; exercises contém somente as séries realizadas atuais. Estes campos também aparecem no GET/POST de detalhe. GET /sessions acrescenta volumeKg aos resumos.

400: dados inválidos; 401: sem sessão; 403: perfil incorreto; 404: registro inexistente ou alheio; 409: versão/identificador de operação em conflito; 503: serviço indisponível. Nenhum campo extra studentId, planId, planVersion ou startedAt troca a origem do registro. As rotas mantêm no-store e HTTPS fora de Development. Não existe DELETE de sessão nesta etapa.

## Como verificar

Execute `python3 scripts/test-native-mysql.py`. `WorkoutCorrectionTests.cs` verifica correção após excluir ficha, totais, persistência, isolamento entre contas, limites, concorrência, reenvio e proteção contra desfazer edições recentes.

No simulador, corrija um treino fictício, inclua/remova séries, salve e reabra o app. Compare duração, observações, séries e volume. Para testar rede, mantenha a edição aberta, encerre somente o helper local, tente salvar, reinicie o helper e use a mesma ação de nova tentativa.

Resultados observados e limites em [evidências](evidencias/correcao-treinos.md).

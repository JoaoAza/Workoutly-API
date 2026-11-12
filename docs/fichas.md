# Fichas de treino

FICHA-01 implementa o planejamento do treino, integrado ao MySQL pela API. Aluno cria e gerencia as próprias fichas; professor/admin gerencia as fichas dos alunos atualmente vinculados. A execução com cargas, duração e observações foi implementada em TREINO-01; veja treinos.md.

## Como usar

1. Inicie `Executar-local.command` e entre no aplicativo.
2. Aluno: **Minhas fichas → Nova ficha**. Professor: **Montar fichas → Fichas do aluno → Nova ficha**.
3. Informe nome e observações opcionais. Em **Adicionar exercício**, busque no catálogo e toque no movimento. É possível cadastrar um exercício próprio nessa seleção e retornar sem perder o formulário.
4. Informe séries e repetições previstas. Use **Subir**, **Descer** ou **Remover** para organizar a sequência. A inclusão começa com 3 séries e 10 repetições editáveis, sem recomendação clínica.
5. Toque em **Salvar ficha**. A lista usa a quantidade real de exercícios e séries; não estima duração nem recomenda dias de descanso.
6. **Abrir ficha** permite editar, recarregar a versão salva ou excluir após confirmação. Recarregar substitui os campos somente após uma resposta bem-sucedida. Voltar pela navegação descarta alterações não salvas; não há rascunho offline persistente.

Se a conexão falhar, a tela conserva os campos e permite tentar novamente. Isso preserva o formulário na página aberta; encerrar o app não guarda o rascunho. Se outra pessoa já editou a ficha, a API recusa sobrescrever a versão mais nova e o app orienta a recarregar ou voltar à lista.

## Conceitos, decisões e código

**Ficha e sessão são diferentes.** A ficha descreve o que se pretende fazer. A sessão futura registrará o que foi realizado. `PlansPage.xaml` lista fichas; `PlanPage.xaml` e seu code-behind editam nome, observações e exercícios ordenados. `PlanExercisePickerPage` usa o catálogo já existente e oculta movimentos incluídos. `Services/AccountClient.cs` envia as operações autenticadas com a sessão do SecureStorage.

**O aluno é o proprietário.** `Plans/PlanEndpoints.cs` aplica políticas student/teacher. O ID do aluno vem da sessão nas rotas pessoais ou do caminho validado nas rotas de professor; campos extras no corpo não substituem o proprietário. `PlanService.CanAccess` verifica o vínculo atual no banco dentro da transação. Remover o vínculo retira o acesso do professor, inclusive a versões anteriores, sem apagar fichas do aluno. O professor vinculado pode consultar, editar e excluir fichas do aluno, inclusive aquelas criadas pelo próprio aluno ou por um professor anterior. Não há acesso global de administrador.

**Revisões imutáveis e exclusão lógica.** A migração `005_workout_plans.sql` eleva o esquema à versão 5:

| Tabela | Responsabilidade |
| --- | --- |
| workout_plans | Identidade, aluno proprietário, versão atual e data de exclusão. |
| workout_plan_versions | Nome, observações, autor e data de cada revisão. |
| workout_plan_items | Ordem, séries, repetições e cópia dos dados dos exercícios em cada revisão. |

Editar insere uma revisão e seus itens em uma única transação; revisões anteriores não são reescritas. Excluir preenche deleted_at e retira a ficha das rotas de uso, preservando revisões e itens no banco. As sessões de TREINO-01 referenciam a revisão executada, inclusive depois de excluir a ficha. O registro preserva os exercícios e metas originais; indicadores e acompanhamento pelo professor permanecem pendentes.

**Exercícios privados e cópia na ficha.** Uma inclusão nova aceita somente catálogo compartilhado ou exercício do autor autenticado. Se o professor inclui um exercício próprio, o aluno recebe nome, grupo, equipamento e descrição desse movimento dentro da ficha; o catálogo privado do professor permanece fechado. Itens já presentes podem ser mantidos ou ajustados pelo aluno/professor vinculado sem consultar o catálogo privado alheio. Remover esse item e depois tentar adicioná-lo novamente exige acesso ao catálogo de origem; a lista de seleção não expõe os exercícios privados de outras contas. As cópias existentes não mudam com futuras alterações do catálogo.

**Concorrência e reenvio.** Alteração e exclusão informam a versão lida. Duas edições da mesma versão não se sobrescrevem: uma salva e a outra recebe 409. A transação também impede gravação parcial de uma ficha com exercício indisponível. Cada página nova gera um UUID para a criação; reenviar o mesmo ID e conteúdo da primeira versão retorna a ficha existente, sem duplicar. O ID não é reutilizado depois da exclusão. Essa proteção cobre reenvios na página aberta, sem prometer sincronização offline.

## Contratos HTTP

Todas as rotas exigem sessão e usam no-store; fora de Development exigem HTTPS. O aplicativo não acessa MySQL diretamente.

| Operação | Aluno | Professor/admin vinculado |
| --- | --- | --- |
| Listar | GET /plans | GET /teaching/students/{studentId}/plans |
| Consultar atual | GET /plans/{id} | GET /teaching/students/{studentId}/plans/{id} |
| Consultar revisão ativa | GET /plans/{id}?version=1 | GET /teaching/students/{studentId}/plans/{id}?version=1 |
| Criar | POST /plans | POST /teaching/students/{studentId}/plans |
| Editar | PUT /plans/{id} | PUT /teaching/students/{studentId}/plans/{id} |
| Excluir | DELETE /plans/{id}?version=2 | DELETE /teaching/students/{studentId}/plans/{id}?version=2 |

Criação:

```json
{
  "id": "UUID gerado para esta criação",
  "name": "Treino A",
  "notes": "Observações opcionais",
  "exercises": [{ "exerciseId": "UUID de exercício disponível", "sets": 3, "repetitions": 10 }]
}
```

Na edição, o corpo troca id por `version`, inteiro da versão atual; o ID fica no caminho. A resposta traz id, studentId, name, notes, version e exercises em ordem. Cada item inclui exerciseId, name, muscleGroup, equipment, description, sets e repetitions. Os dados descritivos vêm do servidor; não são aceitos como substituições no corpo.

Validação: nome de 2 a 120 caracteres; observações até 1000; de 1 a 50 exercícios distintos; de 1 a 20 séries e de 1 a 200 repetições por exercício. Esses limites restringem entrada de dados; não são orientação de prescrição.

Status: 201 criado; 200 consultado/editado ou mesma criação reenviada; 204 excluído; 400 dados inválidos/exercício indisponível; 401 sem sessão; 403 papel incorreto; 404 ficha inexistente, excluída ou fora do acesso; 409 conflito de versão/ID; 503 serviço indisponível. Lista de aluno sem fichas retorna 200 com array vazio. Lista de professor sem vínculo retorna 404. Ficha excluída não pode ser consultada pelas rotas acima, mesmo com version.

## Como testar

Execute `python3 scripts/test-native-mysql.py`. Os testes HTTP de `PlanTests.cs` exercitam CRUD, reenvio, reinício, revisões, validação, exercícios privados, concorrência, dois alunos e dois professores, além da perda de acesso ao remover o vínculo. O runner usa base temporária isolada; nenhum teste apaga o banco local de demonstração.

No simulador, crie como professor, alterne para o aluno e abra a mesma ficha; edite séries, salve, feche/reabra o app e confira a recuperação. Teste a exclusão somente com uma ficha descartável. Para conferir erro de conexão, mantenha o formulário aberto, encerre apenas o ambiente local com Ctrl+C, tente salvar, reinicie o ambiente e tente novamente.

Resultados observados e limites estão em [evidências](evidencias/fichas.md).

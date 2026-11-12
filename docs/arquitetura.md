# Arquitetura do Workoutly

## O que existe atualmente

`Workoutly.Mobile` possui telas de cadastro, login e conta em MainPage.xaml/code-behind. ExercisesPage lista e busca exercícios; ExercisePage permite consultar detalhes ou cadastrar um exercício privado. AccountClient realiza HTTP e guarda apenas sessão e servidor no SecureStorage; a senha não é persistida. AppShell organiza a navegação.

A identidade visual vem do Figma do grupo. Colors.xaml concentra a paleta e Styles.xaml os padrões de texto, botões, campos e cartões. MauiProgram registra a fonte Inter e remove os contornos nativos dos campos para usar os Border XAML. Os controles permanecem nativos; regras e contratos HTTP não dependem dos estilos. Detalhes em identidade-visual.md.

O perfil faz parte de AccountProfile e vem da API. ConnectionsPage apresenta Meus alunos para professor/admin ou Meu professor para aluno. A migração 004 adiciona account_roles, student_teachers e teacher_invitations, registrando a versão 4 do esquema. Claims de papel são reconstruídas pelo SessionAuthenticationHandler a partir do banco a cada requisição. As políticas teacher/student protegem as rotas de vínculo; a consulta SQL restringe a propriedade da relação. Essa base também autoriza as fichas dos alunos vinculados; acompanhamento das sessões e indicadores também exigem vínculo ativo e são somente de leitura para o professor. Detalhes em perfis-e-vinculos.md.

`Workoutly.Api` possui `GET /health` para verificar o processo e `GET /health/ready` para consultar a versão persistida no MySQL. A conexão é configurada por variável de ambiente, somente no servidor. O comando `--migrate` prepara o histórico de versões; abrir a API não modifica o banco. As rotas /auth/register, /auth/login, /auth/me e /auth/logout usam as tabelas users e user_sessions. As rotas autenticadas /exercises usam a tabela exercises: owner_id nulo identifica o catálogo inicial, e owner_id preenchido limita acesso à conta proprietária. A migração 003 registra o esquema na versão 3. A migração 005 adiciona workout_plans, workout_plan_versions e workout_plan_items; a migração 006 adiciona workout_sessions/workout_session_sets e registra a versão 6. A migração 007 adiciona workout_session_changes e elevou o esquema a 7; a migração 008 do catálogo ampliado estabeleceu a versão 8. As rotas /sessions registram, consultam e corrigem somente sessões do aluno autenticado. Detalhes e comandos em mysql.md e exercicios.md.

`Workoutly.Api.Tests` inicia a aplicação com WebApplicationFactory e realiza uma requisição HTTP de verdade no servidor de testes em memória. Os testes HTTP sem banco verificam serialização, status e estados de falha. O teste Category=MySql verifica a migração e sua persistência em uma base MySQL real e aleatória, isolada dos dados da aplicação. O script nativo também verifica Kestrel por TCP local e reinício da API. Sem configuração de teste, o teste MySql é ignorado explicitamente, sem alegar validação real.

## Fluxo atual

O usuário interage com o app MAUI. O app envia requisições HTTP para contas, vínculos, exercícios, fichas, sessões de treino e histórico com indicadores. A API valida os dados, autentica, autoriza as consultas e persiste os registros no MySQL. Os próximos recursos seguirão esse fluxo. Somente a API conhece a conexão com o banco.

Esses três componentes são separados porque a interface muda sem exigir conexão direta ao banco e as regras de acesso ficam centralizadas. Os futuros contratos serão documentados conforme cada recurso for implementado; não há contratos simulados de cadastro ou treino nesta base.

## Conexão e histórico

A versão final exige conexão com a API. Na ausência de rede, o formulário em andamento será preservado para nova tentativa. Não haverá sincronização offline nesta versão. Fichas possuem revisões imutáveis e itens com cópia dos dados do exercício. Edição insere revisão; exclusão é lógica. A transação valida propriedade/vínculo e versão para impedir sobrescrita concorrente. As sessões referenciam a revisão executada e preservam os dados mesmo após alterações ou exclusão da ficha. Ver fichas.md.

PlansPage lista fichas; PlanPage edita o planejamento e PlanExercisePickerPage seleciona exercícios do catálogo existente. PlanService persiste a revisão completa em transação e verifica o vínculo ativo no servidor. O aluno mantém suas fichas após desvincular o professor.

WorkoutPage registra séries realizadas e mede a duração; WorkoutsPage e WorkoutDetailsPage recuperam os dados salvos. WorkoutService grava a sessão e suas séries em transação, compara o ID/conteúdo de reenvios e consulta o aluno pelo token. O treino concluído em memória mantém dados/duração para tentar novamente após falha. Não há persistência de rascunho nem sincronização offline. Detalhes em treinos.md.

WorkoutEditPage corrige as séries da revisão original, duração e observações. O PUT /sessions/{id} valida version e editId; workout_session_changes identifica operações aceitas e não mantém snapshots dos valores anteriores. Séries e cabeçalho são atualizados em transação, e volume/contagens são calculados a partir dos registros atuais. A correção não troca início, proprietário ou revisão da ficha. Ver correcao-treinos.md.

WorkoutsPage agora consulta /history por período e apresenta indicadores, frequência e evolução por exercício. WorkoutHistoryService lê sessões e séries em uma transação consistente, sem gravar totais ou gráficos em tabelas separadas. Professor acessa /teaching/students/{studentId}/history e detalhes das sessões somente enquanto existir o vínculo protegido na mesma transação. Esse incremento usou o esquema 7; a ampliação posterior do catálogo estabeleceu a versão 8. Armazenamento local, fórmulas e contratos estão em historico-e-armazenamento.md.

AppShell coordena a confirmação de saída nos retornos canceláveis das páginas que implementam IConfirmLeave. Cada página compara seus campos para decidir se precisa confirmar. FormFeedback acompanha o tamanho das mensagens e revela os erros no ScrollView; listas distinguem carregamento, falha e vazio. MainPage permite repetir a recuperação de sessão sem novo login em falha de conexão. Esses estados são da interface e não alteram a API ou o esquema. Detalhes em experiencia-de-uso.md.

## Aprendizado deste incremento

- **Solução**: agrupa os projetos para a IDE; não é um servidor nem o aplicativo.
- **Aplicativo MAUI**: gera apps nativos de cada plataforma a partir de C# e XAML.
- **API**: processo independente que responde a requisições HTTP entre o app e o banco.
- **Teste de integração**: verifica o comportamento do endpoint a partir de uma requisição, não chama diretamente a função da rota.
- **Git**: registra versões de código e documentação; publicar no GitHub é uma ação separada.
- **Skill**: procedimento lido pelo agente quando apropriado. Não faz parte do aplicativo que o usuário instala.

## Catálogo ampliado e exercícios das fichas

A migração 008 amplia o catálogo compartilhado de dez para 30 movimentos e registra o esquema 8. `ExerciseService` inclui as cópias dos movimentos privados disponibilizados nas fichas atuais do próprio aluno e retorna `isOwned` e `isInPlan`. O filtro de exercícios próprios considera os dois indicadores. O acesso não depende de abrir o catálogo privado do professor. Imagens locais são apresentadas na lista, no seletor e nos detalhes, com atribuição completa em uma página de créditos lida do pacote do aplicativo.

## Revisão das descrições musculares

A migração 009 inclui anatomia funcional nas descrições dos 30 exercícios compartilhados e estabelece o esquema atual 9. O marcador da migração impede reaplicação dos textos sobre edições posteriores. A atualização dos exemplos privados foi limitada ao banco de demonstração. As fichas ativas desse ambiente receberam novas revisões com os textos atuais; as revisões antigas e suas sessões não foram reescritas. A interface consome o campo `description` já existente, sem alteração do contrato HTTP ou do aplicativo.

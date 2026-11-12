# Workoutly

## Atualização PostgreSQL/Render — 12/11/2026
- Esta versão usa PostgreSQL/Npgsql na API, substituindo MySQL por solicitação do usuário.
- Siga INICIAR-RENDER.md e README.md para execução. As referências MySQL abaixo são históricas.
- Verificações atuais: dotnet build src/Workoutly.Api/Workoutly.Api.csproj e dotnet test tests/Workoutly.Api.Tests/Workoutly.Api.Tests.csproj. Integração real requer WORKOUTLY_TEST_ADMIN_CONNECTION apontando exclusivamente para PostgreSQL local isolado.
- Não executar os scripts em docs/legado-mysql nem recriar o banco remoto já populado.
- Migrações continuam explícitas (--migrate), nunca no startup. Preservar senhas com PasswordHasher, tokens opacos e regras de propriedade e revisão.


## Acordos do projeto
- Responda em português e explique o conceito, a decisão e a verificação ao concluir cada incremento.
- Implementação nova: não reutilizar o aplicativo antigo. Documentos acadêmicos na pasta superior são fontes de requisitos e não devem ser editados sem pedido específico.
- Stack: .NET 10, MAUI com XAML e code-behind, API ASP.NET Core e MySQL somente no servidor.
- Cadastro, login, perfil, logout e catálogo de exercícios estão integrados ao MySQL pela API; o app usa SecureStorage para a sessão. Exercícios iniciais são compartilhados e cadastros próprios são privados. Fichas próprias e de alunos vinculados estão implementadas com revisões; registro e consulta de sessões próprias estão implementados. Correção de sessões próprias também está implementada; histórico por período, indicadores e consulta somente de leitura pelo professor vinculado também estão implementados. Não apresentar planejamento como resultado.
- Implementar o menor incremento completo pedido; evitar abstrações antecipadas, serviços vazios e recursos fora do MVP.
- Identidade visual: seguir o Figma fornecido e docs/identidade-visual.md. Usar cores e estilos em Resources/Styles, fonte Inter e recursos locais originais; não voltar ao tema roxo padrão. Dados exemplificativos do protótipo não são registros reais.
- Funcionalidades finais online; preservar formulário em falhas de rede. Não implementar sincronização offline. UX-01 usa IConfirmLeave no AppShell para retornos canceláveis e FormFeedback para revelar erros após layout. Não confundir lista vazia com falha de carregamento nem apresentar campos em memória como rascunhos persistentes. Seguir docs/experiencia-de-uso.md.
- Autenticação usa PasswordHasher do ASP.NET Core Identity e tokens opacos revogáveis, guardados somente como hash no servidor. Não substituir por hash simples de senha ou sessão em Preferences.
- Dois perfis: student (aluno) e teacher (professor). O cadastro público permite escolher um desses perfis; a API aceita exclusivamente esses dois valores e grava o papel no MySQL. O papel efetivo vem do MySQL a cada requisição, não apenas da seleção no cliente. Professor acessa somente alunos vinculados; manter essa regra ao implementar fichas e treinos.
- Fichas pertencem ao aluno. Edição cria revisão imutável; exclusão é lógica. Sessões referenciam a revisão executada sem depender da versão atual. Seguir docs/fichas.md, docs/treinos.md e docs/correcao-treinos.md. Não alterar a identificação de uma tentativa ao reenviar o mesmo treino.
- Correção de treino mantém início e revisão da ficha. Validar version e editId para impedir sobrescrita concorrente ou repetição da mesma correção. Totais vêm das séries atuais, sem cache independente. Histórico usa datas e offset UTC explícito, até 366 dias, leitura consistente e vínculo ativo para o professor. Seguir docs/historico-e-armazenamento.md.
- Não embutir credenciais ou dados pessoais. Somente dados fictícios nos testes automatizados.
- Ler docs/backlog.md e docs/arquitetura.md antes de alterar o escopo. Usar GLOSSARY.md para os termos.
- Não adicionar integrações sociais, wearables, prescrição clínica ou AI Coach ao MVP.

## Verificações
- API: `dotnet build src/Workoutly.Api/Workoutly.Api.csproj`.
- Integração HTTP: `dotnet test tests/Workoutly.Api.Tests/Workoutly.Api.Tests.csproj`.
- Persistência real: `python3 scripts/dev.py test-db` com o MySQL Compose disponível, ou `python3 scripts/test-native-mysql.py` neste Mac. Sem banco, o teste MySql é ignorado explicitamente.
- Preparar esquema com `python3 scripts/dev.py migrate`; nunca executar DDL automaticamente no startup. Não imprimir conexões nem incluir credenciais no Git.
- iOS no Mac: `dotnet build src/Workoutly.Mobile/Workoutly.Mobile.csproj -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64`.
- Android: `dotnet build src/Workoutly.Mobile/Workoutly.Mobile.csproj -f net10.0-android` (requer JDK 21 e Android SDK).
- Compilação não prova execução. Registrar a plataforma e os limites da verificação em docs/evidencias/.
- Testar comportamento e contratos públicos, sem testes que apenas repitam a implementação.

## Agent skills
- Skills locais em .agents/skills; origem e revisão em docs/skills.md. Ler o SKILL.md aplicável antes de usar uma skill.
- Tarefas em docs/backlog.md, com IDs e dependências. Não publicar issues, PRs ou mensagens externas automaticamente.
- Vocabulário em GLOSSARY.md; decisões em docs/adr/. Configuração em docs/agents/.
- Usar tdd para regras e contratos novos quando houver comportamento a testar; diagnosing-bugs para falhas difíceis; code-review para revisão solicitada.
- diagram-design para visuais que ajudem a explicar o sistema; definir o perfil visual antes do primeiro diagrama.
- Usar subagentes quando solicitado ou quando as instruções da skill aplicada exigirem delegação.

# Workoutly — situação atual e aderência aos roteiros

**Data do balanço:** 21/10/2026. **Código atual:** `64cb525`, obtido de [pedrobaldi01/Workoutly](https://github.com/pedrobaldi01/Workoutly), branch `documentacao/situacao-fase-2`. **App:** 0.11.0 (11). **Base com execução documentada:** tag `v0.11.0-experiencia-uso`, commit `bf5e0a6`. **Esquema MySQL:** 7.

Este documento compara as solicitações feitas durante o desenvolvimento, o roteiro inicial e o relatório da Fase 2 com a implementação e as evidências locais. Não substitui as entregas anteriores nem constitui o relatório final de avaliação.

## 1. Diagnóstico atual

O Workoutly já possui uma implementação nova em **C#/.NET MAUI + API ASP.NET Core + MySQL**, com os fluxos essenciais de conta, exercícios, fichas, execução, correção de treinos, histórico e indicadores. Também há dois perfis, aluno e professor/admin, com acompanhamento limitado aos alunos vinculados.

A integração da base `bf5e0a6` foi exercitada no simulador de iPhone e por testes HTTP com MySQL real. A última execução registrada para essa base apresentou **30 testes aprovados, zero falhas e zero ignorados**; as compilações de iOS e Android terminaram sem avisos ou erros. Esses resultados pertencem à verificação técnica interna, com dados fictícios.

Durante esta documentação, foi integrado o commit do GitHub `64cb525`, que acrescenta escolha do perfil no acesso/cadastro e ilustrações dos exercícios. As mudanças estão presentes no código local, mas a suíte e o aplicativo desse commit não foram reexecutados nesta atualização. Os resultados da base anterior não devem ser apresentados como validação automática dessas novas mudanças.

**O objetivo acadêmico geral ainda está parcialmente atendido:** a implementação avançou além da situação descrita na Fase 2, mas faltam a validação completa nas plataformas, a avaliação com três participantes reais, a análise desses dados e a consolidação da documentação/evidências para a entrega. Não há base para afirmar melhora de constância, satisfação ou facilidade de uso do público-alvo.

### Como interpretar os estados

- **Implementado:** comportamento presente no código, com evidência técnica identificada. Não significa validado em todos os aparelhos ou avaliado pelo público.
- **Parcial:** parte da entrega existe, mas falta uma comprovação ou artefato exigido.
- **Pendente:** não foi localizado resultado executado no novo repositório para a atividade indicada.
- **Fora do escopo:** não é condição de conclusão do MVP aprovado.

Não foi calculada uma porcentagem de conclusão, pois código, documentação, execução em dispositivos e avaliação com pessoas são entregas distintas.

## 2. Fontes e limites da comparação

| Fonte | Papel neste balanço |
| --- | --- |
| [Roteiro Workoutly.docx](../../Roteiro%20Workoutly.docx), seções 1–4.6 | Define problema, objetivos, stack, funcionalidades, metodologia e critérios de sucesso. |
| [Roteiro Fase 2 - Workoutly.docx](../../Roteiro%20Fase%202%20-%20Workoutly.docx), seções 1–11 | Registra protótipo e backend inicial e organiza o que deve ser apresentado como versão, testes, resultados, discussão, conclusões e evidências. |
| Solicitações do desenvolvimento | Reinício do zero; pasta/repositório próprios; identidade do Figma; dois perfis; professor monta fichas e acompanha alunos vinculados; desenvolvimento incremental com explicações. |
| [Backlog](backlog.md), [arquitetura](arquitetura.md), código e testes | Permitem conferir o comportamento implementado e as pendências atuais. |
| [Evidências de UX-01](evidencias/experiencia-de-uso.md) e demais evidências por versão | Registram ambiente, comandos, resultados observados, capturas e limites de cada incremento. |
| [Referências](referencias.md), [identidade visual](identidade-visual.md) e [skills](skills.md) | Registram fontes de apoio, consulta anterior ao Figma e limites de reutilização. |

Foram lidos os parágrafos e tabelas dos DOCX originais, sem editá-los. Os Markdown da pasta superior são auxiliares de leitura. A imagem do roteiro inicial apresenta um fluxo genérico do Projeto Integrador; não representa arquitetura ou modelo de dados do Workoutly.

A comparação utiliza os arquivos locais, os registros de consultas anteriores e a leitura do repositório GitHub fornecido pelo usuário. Não houve nova leitura do Figma, execução de testes nem coleta com participantes durante este balanço. Resultados de testes citados são os últimos documentados e versionados. Materiais externos que ainda não foram disponibilizados ao novo repositório podem complementar esta análise.

## 3. O que foi feito em relação ao que foi pedido

| Pedido / requisito | Situação e resultado atual | Evidência principal / limite |
| --- | --- | --- |
| Refazer do zero, sem usar o app anterior | Implementado. Novo projeto em `Workoutly`, com repositório próprio, solução, app, API, testes e documentação. | [Fundação](evidencias/fundacao.md); o aplicativo antigo na pasta superior não integra esta solução. |
| Preservar documentos enviados ao professor | Atendido. Os dois DOCX e os dois auxiliares Markdown permanecem na pasta superior; hashes conferidos ao final dos incrementos. | [Fundação: integridade](evidencias/fundacao.md#integridade-dos-documentos-originais). |
| Usar .NET MAUI, API .NET e MySQL | Implementado. Interface nativa C#/XAML, regras/autorização na API e banco acessado exclusivamente pelo servidor. | [Arquitetura](arquitetura.md), `src/Workoutly.Api`, `src/Workoutly.Mobile` e migrações em `Data/Migrations`. |
| Cadastro, login e encerramento seguro da sessão | Implementado. Cadastro público permite aluno ou professor na revisão `64cb525`; senha com hash; token opaco revogável; sessão no SecureStorage; logout revoga o acesso. | [Autenticação](autenticacao.md), [evidências](evidencias/autenticacao.md), `AuthenticationTests` e `AccountValidationTests`. |
| Conta aluno e conta professor/admin | Implementado. Dois perfis identificados pela API. Na base validada, professor era criado pelo operador; em `64cb525`, a primeira tela e o cadastro permitem escolher aluno ou professor. A API aceita somente student/teacher e persiste o papel. O fluxo atualizado precisa de nova validação. | [Perfis e vínculos](perfis-e-vinculos.md), [evidências](evidencias/perfis-e-vinculos.md) e `RoleTests`. |
| Professor montar fichas e acompanhar alunos vinculados | Implementado. Vínculo voluntário por código, um professor ativo por aluno; gestão das fichas e leitura de sessões/indicadores. Remover vínculo bloqueia novas consultas e preserva os dados do aluno. | `PlanTests`, `WorkoutHistoryTests` e [histórico](evidencias/historico.md). Professor não tem acesso global nem corrige sessões dos alunos. |
| Cadastro ou seleção de exercícios | Implementado. Dez exercícios iniciais compartilhados, cadastro privado, busca e seleção para ficha. O commit `64cb525` acrescenta ilustrações SVG e dicas nos detalhes dos exercícios iniciais. | [Exercícios](exercicios.md), [evidências](evidencias/exercicios.md) e `ExerciseTests`. |
| Criar, consultar, editar e excluir fichas | Implementado. Exercícios ordenados, metas de séries/repetições, revisões imutáveis e exclusão lógica. | [Fichas](fichas.md), [evidências](evidencias/fichas.md) e `PlanTests`. |
| Registrar séries, repetições, cargas, duração e observações | Implementado. O aluno registra o realizado, marca as séries feitas e salva uma sessão ligada à revisão executada. Reenvio da mesma tentativa não duplica o treino. | [Treinos](treinos.md), [evidências](evidencias/treinos.md) e `WorkoutSessionTests`. |
| Corrigir registros próprios | Implementado. Correção de séries, cargas, repetições, duração e observações, com controle de versão e reenvio. | [Correção](correcao-treinos.md), [evidências](evidencias/correcao-treinos.md) e `WorkoutCorrectionTests`. Início, proprietário e revisão da ficha não mudam. |
| Histórico, frequência e evolução | Implementado. Período por datas, sessões, dias ativos, séries, repetições, duração, volume e maior carga por exercício em cada sessão. | [Histórico e armazenamento](historico-e-armazenamento.md), [evidências](evidencias/historico.md) e `WorkoutHistoryTests`. Não há avaliação clínica ou estimativa de força máxima. |
| Adaptar ao Figma e à identidade visual do grupo | Implementado conforme frames consultados e registrados. Paleta escura, verde-lima/turquesa, Inter, marca e cartões aplicados aos controles nativos. | [Identidade visual](identidade-visual.md), [evidências](evidencias/identidade-visual.md) e `docs/design/manifest.json`. Novos fluxos reutilizam os padrões; não foi feita nova comparação completa com o arquivo remoto nesta auditoria. |
| Uso online compreensível e prevenção de perda de campos | Implementado no recorte de UX-01. Confirmação ao sair, carregamento/erro/vazio distintos, tentativa de recuperação de acesso e mensagens de validação. | [Experiência de uso](experiencia-de-uso.md) e [evidências](evidencias/experiencia-de-uso.md). Campos em memória não são rascunhos persistentes. |
| Usar openGym e os repositórios de skills como apoio | Atendido como referência e apoio de desenvolvimento. openGym orienta experiência; não houve adoção de React/Node como base. Onze skills locais estão disponíveis com revisões e licenças registradas. | [Referências](referencias.md), [skills](skills.md), `docs/skills.lock.json`. Instalação não comprova execução de todos os workflows; diagram-design disponível não equivale a diagramas acadêmicos concluídos. |
| Ilustrações de terceiros | Código e recursos presentes em `64cb525`, com avisos de atribuição ao Workout Guide/CC BY-SA 4.0. Não foram conferidos em execução nesta atualização. | `Services/ExerciseVisuals.cs`, `Resources/Images/Exercises/`, [avisos de terceiros](../THIRD-PARTY-NOTICES.md). Na documentação final, manter procedência e licenças junto das figuras utilizadas. |
| Explicar o funcionamento por incremento | Atendido na documentação técnica: conceito, decisão, arquivos e verificação por tema. | Arquitetura, autenticação, exercícios, fichas, treinos, correção, histórico e UX em `docs/`. A compreensão e revisão pelo grupo ainda precisam ser confirmadas para a apresentação. |

### Armazenamento e execução atuais

Os dados persistentes ficam no MySQL; o app usa a API. No caminho nativo deste Mac, o banco `workoutly` fica em `.local/native-mysql/data`, fora do Git, e é preservado ao encerrar os serviços. Treinos e séries ficam em tabelas próprias; indicadores são calculados sobre os registros atuais. As revisões da ficha preservam o planejamento executado. A tabela de correções identifica operações e versões, sem guardar cópia integral dos valores antigos.

A API usada nas demonstrações está em `localhost:5080`; o simulador iOS acessa esse endereço. Android em emulador usa `10.0.2.2`. Telefone físico precisa de uma API HTTPS acessível a ele. Ainda não há hospedagem ou backup automático configurados no projeto. O Git guarda código/documentação; um clone não recupera os dados do banco. Detalhes em [Histórico e armazenamento](historico-e-armazenamento.md).

## 4. Evolução em relação ao relatório da Fase 2

As afirmações da Fase 2 descrevem a entrega histórica fornecida. Não se deve substituir retroativamente seus resultados pelos testes executados depois. Na próxima redação, apresentar explicitamente a evolução até a versão 0.11.0.

| Seção da Fase 2 | Situação descrita no arquivo | O que já mudou / o que falta para a nova redação |
| --- | --- | --- |
| 1 — Planejamento e objetivos | Design e backend inicial; objetivos parcialmente alcançados. | Reavaliar cada objetivo com a matriz da seção 5 deste balanço. Objetivo geral continua parcial pela ausência da avaliação com público-alvo. |
| 2.1 — Solução e funcionalidades | Consulta de registros representada no Figma; integração pendente. | Já existem fluxos executáveis integrados. Substituir o inventário na próxima entrega por funcionalidades, comportamento, versão e evidências, respeitando limites de plataforma. |
| 2.2 — Arquitetura | Stack prevista; integração ainda sem comprovação. | Arquitetura implementada, migrações e leitura/gravação demonstradas. Faltam consolidar os diagramas de arquitetura/modelo de entidades exigidos pelo roteiro inicial. |
| 2.3 — Versões | Sem tag/commit executável identificado. | Onze marcos locais de produto, de `v0.1.0-base` a `v0.11.0-experiencia-uso`. Registrar versão de demonstração e motivo das alterações, sem atribuí-las a avaliações com alunos que não ocorreram. |
| 3 — Testes e validação | Instrumentos planejados; nenhum participante e ambiente não registrado. | Há ambiente e procedimentos técnicos internos. Preparar consentimento, entrevista, tarefas, ficha de observação e questionário de 1–5; definir ambiente em smartphone para P1/P2/P3. |
| 4 — Testes funcionais | Casos de cadastro, consulta, persistência e métricas não executados. | Os casos técnicos possuem evidências e testes. Consolidar uma tabela por requisito com resultado, ambiente, versão, evidência e limite; concluir QA-01 em iOS/Android. |
| 5 — Avaliação com usuários | Sem medidas, comentários ou dados individuais. | Continua pendente. Trinta testes automatizados não são trinta participantes nem comprovam facilidade de uso. |
| 6 — Análise e discussão | Limitada a proposta, Figma e backend inicial. | Pode discutir integração, consistência dos dados e prevenção de erros observadas tecnicamente. Confrontar resultados do público com objetivos e referencial somente após coleta. |
| 7 — Limitações | MVP integrado inexistente e ausência de testes. | Atualizar a limitação técnica: há versão integrada, mas execução Android, aparelhos físicos, gestos e outros cenários ainda precisam de verificação. A falta de avaliação com três pessoas permanece. |
| 8 — Conclusões | Contribuição restrita ao protótipo e desenvolvimento inicial. | A próxima conclusão pode incluir implementação e testes internos. Não afirmar aumento de constância, satisfação ou adequação ao público sem resultados reais. |
| 9 — Evidências e código no forge | Capturas, resultados, endereço do repositório e versão pendentes. | Capturas e tags locais existem. O endereço público [pedrobaldi01/Workoutly](https://github.com/pedrobaldi01/Workoutly) foi fornecido e o commit remoto `64cb525` foi conferido. Confirmar o acesso do professor e a versão final apresentada; este novo balanço ainda depende de publicação posterior. Selecionar imagens e dar título, fonte e referência no texto final. |
| 10 — Checklist | Vários itens de método, resultados, figuras, código e IA abertos. | Conferir os itens usando este balanço e as evidências atuais. Declaração de IA precisa refletir também o apoio na implementação/testes, além da redação já mencionada. Revisão pelo grupo continua necessária. |
| 11 — Critérios de avaliação | Produto, testes, resultados, discussão, conclusões e coerência. | As entregas técnicas oferecem parte das evidências; análise com participantes, conclusão acadêmica e pacote de entrega ainda não estão completos. |

## 5. Atendimento aos objetivos e critérios de sucesso

### Objetivos específicos do roteiro inicial

| Objetivo | Estado atual | Evidência existente | Pendência para fechar o objetivo |
| --- | --- | --- | --- |
| OE1 — Levantar necessidades, conceitos e requisitos | Parcial | Problema, recorte, conceitos e fichas de fontes no roteiro; glossário, backlog e decisões locais. | Consolidar mapa conceitual específico do Workoutly; revisar a fundamentação e a comprovação das fontes realmente consultadas; registrar a entrevista/necessidades do público prevista na metodologia. |
| OE2 — Projetar experiência, arquitetura e modelo de dados | Parcial | Figma registrado, UI implementada, arquitetura textual, contratos por recurso e migrações relacionais. | Consolidar diagrama de arquitetura e modelo de entidades com relacionamentos, compatíveis com o esquema 7; completar a ligação entre conceitos, dados e decisões. SQL existente não substitui sozinho os artefatos acadêmicos pedidos. |
| OE3 — Implementar o MVP em MAUI, API e MySQL | Implementação essencial entregue; validação parcial | Código versionado, integração e suíte técnica; execução no iPhone Simulator e build Android. | Executar o fluxo completo em Android e repetir a matriz final de QA; resolver falhas que surgirem e fixar a versão candidata usada na avaliação. |
| OE4 — Testar e avaliar com três participantes | Pendente na dimensão com público-alvo | Testes técnicos internos e critérios de avaliação definidos nos roteiros. | Recrutar P1/P2/P3 conforme os critérios, obter consentimento, aplicar instrumentos, analisar resultados e documentar recomendações. |

### Critérios que ainda não podem ser declarados atingidos

| Critério do roteiro, seções 4.5–4.6 | O que é conhecido | O que falta comprovar |
| --- | --- | --- |
| Funcionalidades críticas executadas corretamente | Há casos automatizados e verificações de interface aprovados nos cenários documentados. | Matriz completa da versão final em iOS e Android, com casos esperados, observados e eventuais reprovações. |
| Ausência de falha crítica/perda de dados | Persistência, correções, reenvios e isolamento foram verificados nos testes descritos. | Conferência do fluxo final e dos registros durante a avaliação; não generalizar a ausência de falhas dos testes para todos os ambientes. |
| Três participantes concluírem o fluxo principal sem auxílio | Nenhum resultado de participante foi registrado no novo projeto. | Resultado individual de P1, P2 e P3, distinguindo tarefas concluídas sem ajuda, com ajuda e não concluídas. |
| Média de facilidade ≥ 4/5 | Não aferida. | Aplicar o questionário de 1 a 5 e calcular a média a partir das respostas reais; apresentar também notas individuais. |

O instrumento previsto é uma escala de 1 a 5; **SUS não foi definido** no roteiro. Não alterar o instrumento ou apresentar um resultado SUS retroativamente. Tempos de compilação e valores de carga da demonstração não são medidas de usabilidade ou resultados de prática física.

## 6. Evidências disponíveis e seus limites

| Área | Registro verificável |
| --- | --- |
| Última suíte técnica documentada, base `bf5e0a6` | [UX-01](evidencias/experiencia-de-uso.md): 30 aprovados, 0 falhas, 0 ignorados, com MySQL real disponível em instância temporária isolada. Inclui testes HTTP e de validação. |
| Contas, logout, privacidade e perfis | `AuthenticationTests`, `AccountValidationTests`, `RoleTests`; evidências de [contas](evidencias/autenticacao.md) e [vínculos](evidencias/perfis-e-vinculos.md). |
| Fichas e autorização do professor | `PlanTests`; [fichas](evidencias/fichas.md). |
| Registro, recuperação e reenvio de treino | `WorkoutSessionTests`; [treinos](evidencias/treinos.md). |
| Correções e concorrência | `WorkoutCorrectionTests`; [correção](evidencias/correcao-treinos.md). |
| Indicadores, datas, carga zero e perda de vínculo | `WorkoutHistoryTests`; [histórico](evidencias/historico.md). |
| Uso no simulador e falha de conexão | Capturas e procedimentos por incremento, especialmente [UX-01](evidencias/experiencia-de-uso.md). |
| Preparação do banco | `MySqlBootstrapTests`, sondagens de prontidão e migrações reaplicáveis; [MySQL](evidencias/mysql.md). |

Os totais de testes dos incrementos anteriores são registros históricos de cada versão; não devem ser somados como se fossem suítes independentes. A evidência mais recente informa 30 casos na execução da base `bf5e0a6`. O commit posterior `64cb525` também altera testes de perfis/validação; sua execução não foi presumida.

O ambiente demonstrado usa dados fictícios. O treino com 995 kg de volume, três séries e 3min30s comprova recuperação e cálculo daquele exemplo, sem representar participante ou resultado da avaliação. As verificações de UX adicionaram uma ficha fictícia de navegação; não criaram uma nova sessão de treino.

Android foi compilado, mas sua execução ainda não foi comprovada. Não há verificação final em iPhone físico/Release. Gestos de retorno, botão físico do Android, telas menores, fontes ampliadas e leitores de tela têm limites explícitos. O gráfico com múltiplos pontos foi verificado nos dados da API; a captura do simulador mostra o único treino da demonstração. Testes de autorização não equivalem a auditoria completa de segurança ou desempenho.

## 7. O que falta fazer — plano de fechamento

As atividades abaixo complementam o [backlog](backlog.md). Preparação documental pode ocorrer em paralelo à validação; coleta com participantes deve usar uma versão tecnicamente pronta.

| Ordem / atividade | Ação concreta | Critério de conclusão e entrega |
| --- | --- | --- |
| 1 — DOC-02: consolidar projeto e fundamentação | Revisar mapa de conceitos, fichas de fontes já presentes no roteiro e conexão com o projeto. Produzir diagrama de arquitetura e modelo de entidades do esquema 7. Consolidar referência dos contratos HTTP já descritos por recurso. | Artefatos específicos do Workoutly, fontes verificadas e rastreio até requisitos/dados/código; material pronto para citar nas seções de projeto e referencial. |
| 2 — QA-01: validar a versão candidata | Percorrer cadastro/login com a escolha de aluno/professor, detalhes com ilustrações, ficha, execução, salvamento, correção, reabertura, histórico e logout em iOS e Android; incluir professor, vínculo, falha de rede, dados inválidos e repetição de salvamento. | Matriz por requisito com ambiente/dispositivo, versão, passos, resultado esperado/obtido, evidência e falhas corrigidas ou relatadas. Conferir gestos/retorno, legibilidade e gráfico com múltiplas sessões na interface. |
| 3 — AVAL-01: preparar a avaliação | Criar roteiro de entrevista, consentimento, tarefas, ficha de observação e questionário de facilidade de 1–5. Definir smartphone e API acessível no ambiente controlado. Selecionar três adultos que praticam atividades físicas, usam smartphone e concordam voluntariamente. | Instrumentos revisados, tarefas repetíveis, versão identificada, participantes codificados P1/P2/P3 e ambiente pronto. Não preencher respostas antes da aplicação. |
| 4 — AVAL-01: coletar dados reais | Aplicar o roteiro; registrar tempo por tarefa, conclusão, erros, ajuda, conferência dos dados, notas e comentários. Permitir interrupção voluntária e coletar somente o necessário. | Registros anonimizados por participante e tarefa; nenhuma identidade, senha ou imagem pessoal exposta. Ajuda prestada deve ser registrada, mesmo que contrarie o critério de autonomia. |
| 5 — DOC-01: analisar e concluir | Calcular contagens, taxa de conclusão, tempos e média de facilidade; apresentar os três resultados individuais. Agrupar comentários nas categorias do roteiro e confrontar com os critérios. | Seções 5–8 da próxima redação sustentadas por dados reais, com objetivos atingidos/parciais/não atingidos e limitações. Não generalizar uma amostra de três pessoas. |
| 6 — DOC-01: organizar o pacote acadêmico | Selecionar figuras/tabelas com título e fonte, mencionar cada uma no texto, registrar referências e declarar o uso de IA no escopo real. Revisar consistência entre roteiro, produto e discussão. | Relatório/artigo com evidências ligadas à versão e conteúdo revisado/compreendido pelos integrantes. Este balanço, isoladamente, não conclui DOC-01. |
| 7 — ENTREGA-01: disponibilizar código | Usar o repositório informado, confirmar o acesso do professor e vincular a versão efetivamente apresentada. Publicar a documentação consolidada quando autorizado, excluindo credenciais e dados pessoais. | Endereço já identificado; faltam comprovação do acesso do professor e fechamento da versão/pacote da entrega. Esta documentação e o pull não publicaram alterações no GitHub. |
| 8 — Apresentação posterior | Preparar slides e demonstração com base nos resultados já obtidos e no formato pedido pela disciplina. | Apresentação consistente com o relatório e com o código disponibilizado. Continua adiada conforme decisão anterior. |

### Pontos para conferência do grupo

- **Integrantes:** o roteiro inicial lista John Oliver, Marcel Correa, Miguel Montemezzo e Pedro Baldi; a Fase 2 acrescenta João Azambuja. Confirmar a composição oficial e uniformizar apenas na próxima versão dos documentos, sem modificar o histórico silenciosamente.
- **Fontes:** há fichas no roteiro inicial, mas sua presença não comprova consulta e validação de cada afirmação. Revisar o uso efetivo das referências e as versões técnicas citadas; o ambiente atual utiliza .NET 10 e MySQL 9.6.0.
- **Artefatos de projeto:** não foram localizados diagramas específicos de arquitetura/modelo de dados no novo repositório. Se existirem materiais do grupo fora dele, incorporá-los como referência e conferir aderência ao código antes de refazer.
- **Acesso em smartphone:** `localhost` do Mac não serve como endereço do telefone físico. Definir uma API HTTPS alcançável para a avaliação. Hospedagem em nuvem é uma opção de ambiente, sem constituir nova funcionalidade obrigatória do MVP.
- **Condições de entrega:** o usuário forneceu a URL do forge durante este balanço. A data final e a confirmação de acesso do professor não foram presumidas. A branch padrão observada no GitHub é `documentacao/situacao-fase-2`, e não `main`.

## 8. O que continua fora do MVP

Sincronização offline, redes sociais, wearables, prescrição clínica, AI Coach, lembretes, metas e exportação continuam fora do recorte aprovado. Backup automático, hospedagem permanente e paginação são decisões técnicas futuras, condicionadas ao ambiente/uso, sem transformá-las automaticamente em requisitos acadêmicos novos. Preparar um ambiente acessível e preservar os dados da avaliação continua necessário para realizar o procedimento escolhido.

O professor/admin permanece um perfil único, limitado aos alunos vinculados. Não há previsão de terceiro perfil, acesso global, edição de treinos alheios ou incorporação do código React/Node do openGym.

## 9. Texto-base para a próxima atualização acadêmica

O trecho abaixo resume a base técnica com execução documentada até 21/10/2026. As mudanças de perfil e ilustrações do commit posterior devem ser incluídas com sua própria validação. Deve ser revisado pelo grupo e atualizado após QA e coleta; não altera a Fase 2 já entregue.

> Após a etapa de prototipação e desenvolvimento inicial descrita na Fase 2, o Workoutly recebeu uma nova implementação em C#/.NET MAUI, integrada a uma API ASP.NET Core e a um banco MySQL. A versão 0.11.0 permite cadastro e autenticação, gestão de exercícios e fichas, registro e correção de sessões e consulta de histórico e indicadores. Foram implementados os perfis de aluno e professor/admin, com acesso do professor restrito aos alunos vinculados. A última suíte técnica registrada apresentou 30 testes aprovados com o ambiente MySQL real disponível, e os fluxos documentados foram exercitados no simulador de iPhone. A compilação Android foi concluída, mas sua execução ainda precisa ser validada. A avaliação com três participantes do público-alvo não foi realizada; portanto, os critérios de conclusão autônoma das tarefas e média de facilidade igual ou superior a 4/5 permanecem sem aferição. Os resultados demonstram avanço da implementação e da integração nos cenários testados, enquanto a adequação ao público e as conclusões de usabilidade dependem das próximas etapas de avaliação.

Na declaração de IA, registrar o apoio do ChatGPT/Codex à organização, redação, implementação, testes e correções desta nova versão, conforme o uso efetivo. A revisão, compreensão e validação acadêmica do trabalho são responsabilidade do grupo; sua conclusão não foi presumida neste documento.

## 10. Identificação da versão e preservação das fontes

Base com execução documentada: `bf5e0a6`, tag `v0.11.0-experiencia-uso`. Código atual após o pull: `64cb525`, na branch `documentacao/situacao-fase-2`. As mudanças posteriores de perfil e ilustrações foram lidas no código e ainda precisam de nova validação. Os incrementos técnicos e suas evidências permanecem no Git, desde `v0.1.0-base`. As alterações deste balanço são documentais, sem nova versão do aplicativo.

| Documento original na pasta superior | SHA-256 de referência |
| --- | --- |
| Roteiro Workoutly.docx | `5f967b3447dfb1ab7344226b3014fbfee8fceaa6943982401587d777c9c9c187` |
| Roteiro Fase 2 - Workoutly.docx | `8e524ff1c2fe35742b20637aba8dc099a09667572b2fa0ba4a45a0cffc959537` |
| Roteiro Workoutly.md | `fffda0746178328c4e15fee0e844c5897a4fedac94be31cc93b028435a612614` |
| Roteiro Fase 2 - Workoutly.docx.md | `9a1282b7b3706633d914ded918c9df9f444b0066e3b518be5fe8d6c6c9c76e43` |

Os quatro valores foram conferidos novamente e permanecem idênticos ao finalizar esta atualização documental. Referências históricas em `docs/evidencias/` descrevem a versão indicada em cada arquivo; pendências ali registradas não devem ser confundidas com o estado consolidado deste balanço.

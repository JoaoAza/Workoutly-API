# Backlog do Workoutly

Fonte: Roteiro Workoutly e Roteiro Fase 2 na pasta superior. A Fase 2 descreve uma entrega histórica; não comprova implementação ou testes desta nova versão.

Balanço consolidado em 21/10/2026: [situação atual e aderência à Fase 2](situacao-atual-e-aderencia-fase-2.md), referente ao app 0.11.0: base validada bf5e0a6/tag v0.11.0-experiencia-uso e código atual 64cb525 após pull do GitHub, cujas alterações ainda precisam de nova validação. A consolidação é documental e não encerra a avaliação nem o relatório final.

## Etapa contratada agora

| ID | Incremento | Aceite | Estado |
| --- | --- | --- | --- |
| BASE-01 | Repositório e instruções | Git local, fontes originais intactas, README e AGENTS. | Concluído — evidências em docs/evidencias/fundacao.md |
| BASE-02 | Solução nova | Projetos MAUI, API e testes incluídos na solução, sem código antigo. | Concluído — evidências em docs/evidencias/fundacao.md |
| BASE-03 | Contrato de saúde | GET /health retorna HTTP 200 e explicita ausência de banco; teste HTTP passa. | Concluído — evidências em docs/evidencias/fundacao.md |
| BASE-04 | Ferramentas de apoio | Skills locais, recursos, licenças e revisões registradas. | Concluído — evidências em docs/evidencias/fundacao.md |

## Incremento MySQL concluído

**DB-01 — concluído:** conexão configurada somente na API, bootstrap reaplicável do histórico de migrações, rota de prontidão e teste em MySQL 9.6.0 real. Evidências em docs/evidencias/mysql.md. O ambiente Docker está preparado, mas não executado neste Mac por configuração inicial pendente do Docker Desktop. A validação real usou instância nativa isolada e temporária.

## Conta e acesso concluídos

**AUTH-01 — concluído:** cadastro, login, perfil, restauração de sessão no SecureStorage e logout com revogação MySQL. Doze testes passaram em MySQL real e o fluxo foi exercitado no iPhone Simulator. Evidências em docs/evidencias/autenticacao.md.

**AUTH-02 — concluído:** perfil, exercícios, fichas, sessões e indicadores isolados por conta. Professor consulta somente alunos com vínculo ativo, inclusive detalhes das sessões, sem permissão de correção. Remover o vínculo bloqueia novas consultas e preserva os dados do aluno. Testes com dois professores e duas contas de aluno; evidências em docs/evidencias/historico.md.

## Catálogo concluído

**EX-01 — concluído:** dez exercícios iniciais compartilhados, cadastro privado com validação, busca, filtro de exercícios próprios e detalhes no app. Selecionar um item abre seus detalhes; a inclusão em fichas depende de FICHA-01. Quinze testes HTTP passaram em MySQL real. Cadastro, validação, ausência de conexão e recuperação após reabrir foram verificados no iPhone Simulator. Evidências em docs/evidencias/exercicios.md.

**EX-02 — concluído:** ilustrações SVG locais do projeto Workout Guide (CC BY-SA 4.0) nos detalhes do catálogo, com atribuição versionada. Os dez movimentos iniciais recebem uma referência visual e uma dica curta associadas por ID estável; exercícios privados não recebem imagem ou instrução inferida. Não cria armazenamento ou rota de mídia no servidor.

## Próximos incrementos

**AUTH-03 — concluído:** dois perfis, aluno e professor. A primeira tela permite escolher o perfil e o cadastro público aceita student ou teacher; valores fora dessa lista são recusados pela API. O papel é persistido e fornecido pelo MySQL em cada requisição. As contas anteriores permanecem como aluno.

**VINC-01 — concluído:** código de vínculo com validade de sete dias, renovação, confirmação pelo aluno, consulta dos alunos vinculados e remoção da relação por qualquer parte. Um professor ativo por aluno. Autorização testada com dois professores; evidências em docs/evidencias/perfis-e-vinculos.md. O vínculo autoriza fichas e acompanhamento somente de leitura das sessões e indicadores em HIST-01.

**VISUAL-01 — concluído antes de FICHA-01:** identidade do Figma aplicada às telas existentes de acesso, cadastro, início, catálogo e formulário/detalhes de exercício. Tokens e Inter locais, marca e avatar originais; referências em docs/identidade-visual.md e verificação em docs/evidencias/identidade-visual.md. Não antecipa funções de fichas ou histórico.

| ID | Dependência | Requisito | Critério de aceite |
| --- | --- | --- | --- |
| AUTH-02 | AUTH-01 | Concluído: autorização dos recursos | Perfil, exercícios, fichas, sessões e indicadores isolados; professor restrito ao vínculo ativo. Evidências em docs/evidencias/historico.md. |
| FICHA-01 | EX-01, VINC-01 | Concluído: criar, consultar, editar e excluir fichas | Fichas próprias e de alunos vinculados; ordem, séries, repetições, revisões imutáveis e exclusão lógica. Evidências em docs/evidencias/fichas.md; sessões integradas em TREINO-01; indicadores integrados em HIST-01. |
| TREINO-01 | FICHA-01 | Concluído: registrar sessão | Séries realizadas, cargas decimais, repetições, duração, observações e consulta própria; revisão original preservada e reenvio sem duplicação. Evidências em docs/evidencias/treinos.md. |
| TREINO-02 | TREINO-01 | Concluído: corrigir registros próprios | Edição de séries, repetições, cargas, duração e observações; volume/contagens recalculados; versões e reenvio seguro, sem alterar contas alheias. Evidências em docs/evidencias/correcao-treinos.md. |
| HIST-01 | TREINO-02, VINC-01 | Concluído: histórico e indicadores | Filtro por datas, frequência, dias ativos, séries, repetições, duração, volume e evolução de carga; professor consulta somente alunos vinculados. Evidências em docs/evidencias/historico.md. |
| UX-01 | HIST-01 | Concluído: uso online compreensível | Confirmação de saída com alterações, estados de lista distintos, nova tentativa, recuperação de acesso e erros visíveis; campos preservados em falha de rede. Evidências em docs/evidencias/experiencia-de-uso.md. |
| QA-01 | UX-01 | Validação multiplataforma | Fluxo completo no iOS e Android; registrar dispositivo, ambiente, versão, resultados e limites. |
| AVAL-01 | QA-01 | Avaliação com três adultos | Preparar consentimento, entrevista, tarefas, observação e questionário de 1–5; aplicar com P1/P2/P3 e registrar tempos, erros, ajuda, integridade e comentários reais. |
| DOC-01 | AVAL-01 | Documentação final | Resultados individuais e consolidados ligados às evidências, análise dos critérios de sucesso, limitações, figuras com título/fonte e declaração do uso efetivo de IA revisada pelo grupo. |

Os demais incrementos estão **pendentes**, começando por QA-01. AUTH-02, HIST-01 e UX-01 estão concluídos. Critérios da avaliação: três participantes concluem o fluxo principal sem auxílio, média de facilidade ≥ 4/5 e nenhuma falha crítica ou perda de dados. Se os resultados divergirem, relatar a divergência; nunca preencher resultados fictícios.

## Consolidação acadêmica e entrega

A comparação com os DOCX identificou entregas que precisam ser acompanhadas além do desenvolvimento. São desdobramentos dos objetivos e da seção 9 da Fase 2, sem ampliar as funcionalidades do MVP.

| ID | Dependência | Requisito | Critério de aceite / estado |
| --- | --- | --- | --- |
| DOC-02 | Arquitetura implementada | Consolidar fundamentação e artefatos de projeto | **Pendente.** Mapa conceitual específico, revisão das fichas de fontes já existentes no roteiro, diagrama de arquitetura, modelo de entidades compatível com esquema 7 e índice dos contratos HTTP. Pode avançar em paralelo a QA-01. |
| ENTREGA-01 | Versão a apresentar e destino definido pelo grupo | Código disponível ao professor no forge | **Parcial.** Repositório informado: https://github.com/pedrobaldi01/Workoutly; origin configurado e commit remoto 64cb525 integrado. Faltam comprovar acesso do professor e fixar versão/pacote final. Não publicar automaticamente. |

Antes da redação final, confirmar a lista oficial de integrantes: a Fase 2 acrescenta João Azambuja aos nomes do roteiro inicial. Preservar as entregas antigas e registrar a evolução em nova versão. Os diagramas/instrumentos não foram criados por este balanço; permanecem tarefas a executar. Slides continuam adiados.

## Fora do MVP

Sincronização offline, redes sociais, wearables, prescrição clínica, AI Coach, lembretes, metas e exportação. A apresentação será preparada depois. O Figma do grupo foi fornecido em 20/10/2026 e orienta a interface; backend externo do grupo não foi fornecido.

# Experiência de uso — UX-01

Versão 0.11.0. Este incremento organiza navegação, preservação de campos e comunicação de estados nos fluxos existentes. Mantém XAML/code-behind, controles nativos, Inter e as cores do projeto. Não modifica contratos da API nem o esquema MySQL 7.

## O que mudou para quem usa

| Situação | Comportamento |
| --- | --- |
| Voltar de ficha ou novo exercício com campos preenchidos | Confirmação para descartar ou continuar editando. Cancelar a saída mantém os campos. |
| Voltar de uma correção de treino | Confirmação quando há alteração ou envio pendente. Abrir e fechar sem alterar não gera confirmação desnecessária. |
| Sair de treino em andamento | Confirmação antes de descartar o treino da tela. Consultar o histórico, quando oferecido após falha de envio, não descarta a tentativa. |
| Salvar com sucesso | Retorno à lista/detalhes sem perguntar se quer descartar o que já foi salvo. A lista recupera os dados da API. |
| Operação em andamento | Indicador de atividade e controles temporariamente indisponíveis, evitando ações repetidas. A navegação inicial também bloqueia toques repetidos enquanto abre a página. |
| Carregar fichas, catálogo ou seleção de exercício | Lista oculta durante carregamento; mensagens de carregamento e falha ficam separadas do estado vazio. |
| Falha ao carregar uma lista | Exibe erro e botão Tentar novamente. Não apresenta ausência de fichas/exercícios/alunos como se a consulta tivesse sido concluída. |
| Falha ao atualizar os vínculos | Dados antigos do acompanhamento ficam ocultos até obter uma resposta atualizada. Campos do código do aluno não são apagados. |
| Abrir o app sem conseguir verificar a sessão | Tentar recuperar acesso repete a leitura da sessão protegida. Uma falha de rede não exige informar a senha novamente quando o token continua válido. |
| Erro em formulário longo | Mensagem junto às ações de salvamento e rolagem para revelá-la. O tamanho da mensagem é considerado após o layout, evitando rolar com altura ainda desconhecida. |
| Teclado aberto | Tocar fora dos campos fecha o teclado. Campos de carga e repetições continuam numéricos; e-mail e endereço usam seus teclados próprios. |

As confirmações da ficha também consideram inclusão, remoção e ordem de exercícios e mudanças das metas. Remover um exercício de uma ficha pede confirmação; a alteração só é aplicada no banco depois de salvar a ficha. Recarregar ficha ou treino mantém sua confirmação de substituição dos campos.

## Conceitos e onde aparecem

### Navegação e alterações não salvas

`AppShell.OnNavigating` consulta o contrato local `IConfirmLeave` antes de retornos canceláveis (`Pop` e `PopToRoot`). Um deferral permite aguardar a escolha no diálogo antes de permitir ou cancelar a navegação. Enquanto há confirmação aberta, outra tentativa de voltar é cancelada.

`PlanPage`, `ExercisePage`, `WorkoutPage` e `WorkoutEditPage` implementam esse contrato. Ficha e correção comparam os campos atuais com os valores carregados; o cadastro de exercício verifica se o formulário foi preenchido. Um treino em andamento sempre exige confirmação de saída. Isso centraliza o ponto de navegação e mantém a decisão sobre os campos na própria página.

Após sucesso de gravação, a página libera o retorno. Quando a autenticação expira, o fluxo de acesso leva ao início para entrar novamente. A confirmação não deve bloquear esse retorno obrigatório.

Os testes no iPhone desta etapa exercitaram os botões de retorno e Cancelar edição. O gesto lateral foi tentado, mas não produziu navegação no ambiente; isso não comprova seu tratamento em todas as plataformas. Confirmar gestos e retorno físico do Android faz parte de QA-01.

### Estados de consulta

Uma lista vazia é uma resposta válida da API, enquanto uma falha de rede significa que não foi possível obter essa resposta. `PlansPage`, `ExercisesPage` e `PlanExercisePickerPage` ocultam a lista até a consulta terminar com sucesso. `ConnectionsPage` só reapresenta convite e alunos depois de recuperar ambas as respostas.

Em `MainPage`, `RestoreAccessAsync` permite repetir a recuperação protegida; `OpenAsync` evita abrir telas repetidas com toques rápidos. Esses ajustes não alteram as permissões de aluno/professor, que continuam sendo verificadas pela API.

### Mensagens e layout

`FormFeedback.FollowError` observa mudanças no tamanho do texto do erro e solicita a rolagem no dispatcher da interface. A ficha mostra o erro acima das ações, deixando espaço para ler a mensagem antes de salvar, recarregar ou excluir. O formulário de exercício usa o mesmo posicionamento próximo do salvamento. `Resources/Styles/Styles.xaml` define o fechamento do teclado ao tocar fora dos campos.

## Preservação e limites

Preservar campos significa mantê-los **na página em memória** durante falhas de rede. Fechar/encerrar o app ou confirmar o descarte pode perder um formulário ainda não salvo. Esta versão não grava rascunhos nem sincroniza offline. Expiração da sessão exige novo login; não foi criada transferência de rascunhos entre sessões de acesso.

Treinos e correções continuam repetindo o mesmo identificador/conteúdo de envio quando necessário, conforme treinos.md e correcao-treinos.md. O servidor mantém validação, concorrência e autorização. Não há mudança de propriedade ou acesso global do professor.

## Como verificar

1. Edite o nome de uma ficha e toque em voltar. Escolha Continuar editando e confira o texto preservado. Restaure os valores iniciais e volte: não deve aparecer confirmação de descarte.
2. Crie uma ficha com exercício, interrompa somente os serviços locais, tente salvar e confira os campos. Reinicie `Executar-local.command` e repita a gravação. Deve retornar à lista e mostrar a ficha salva.
3. Na lista, consulte sem conexão: deve aparecer falha de carregamento e Tentar novamente. Um resultado vazio só deve aparecer depois de consulta válida.
4. Envie um exercício com campos obrigatórios vazios. Confira a mensagem completa e que os campos continuam disponíveis para corrigir.
5. Abra uma correção de treino e cancele sem alterações; depois altere um campo e volte. Só o segundo caso deve pedir confirmação. Descartar deve manter o treino salvo sem mudanças.
6. Abra o app com token válido e serviço parado, reinicie o serviço e toque em Tentar recuperar acesso. Deve voltar ao perfil correto sem pedir novamente a senha.

Resultados e capturas estão em [evidências](evidencias/experiencia-de-uso.md). Esta etapa não substitui QA-01 em iOS/Android nem a avaliação com três participantes reais.

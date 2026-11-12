# Identidade visual do Workoutly

Fonte: [Figma do grupo](https://www.figma.com/design/8HCGErKrHaFX0yCqdbu2Iw/Untitled?node-id=0-1), consultado em 20/10/2026. Foram lidos o contexto detalhado e as imagens dos frames **5:2 — Entrar**, **5:3 — Início** e **5:4 — Minhas fichas**. Referências visuais preservadas em design/. O arquivo Figma permanece sem alterações.

## Paleta e tipografia

| Recurso XAML | Valor | Uso |
| --- | --- | --- |
| Canvas | #0A0A0A | Fundo de telas, navegação e splash. |
| Surface | #161B17 | Campos e cartões. |
| SurfaceRaised | #202722 | Ações secundárias, etiquetas e destaques. |
| Outline / CardOutline | #2B332D / #314033 | Contornos de campos e cartões. |
| Accent | #C5F67A | Marca, ações principais e etiquetas. |
| Mint | #4BD9C7 | Detalhes e texto de destaque. |
| TextPrimary | #F5F5F5 | Títulos e conteúdo principal. |
| TextSecondary | #A7B0A8 | Descrições e informações auxiliares. |
| TextField | #DCE3DD | Texto de formulário. |
| Error | #FDA4AF | Validação e falhas; preservado do sistema existente, sem equivalente definido nos frames consultados. |

Fonte **Inter 4.1**, pesos Regular, Medium, SemiBold e Bold, incluídos como arquivos OTF locais. Origem: [distribuição oficial](https://github.com/rsms/inter/releases/tag/v4.1), licença em licenses/Inter-LICENSE.txt. Não depende das fontes instaladas no Mac do desenvolvedor ou de downloads durante o uso.

Títulos de acesso com 34, títulos de página com 28, corpo com 13–15 e etiquetas com 11–12 unidades de layout. Campos com raio 12 e altura mínima 68; cartões com raio 18; botões com raio 14 e altura mínima 50. O acesso utiliza ação de 54 e a marca ocupa 42 × 42. As medidas seguem o design e se adaptam à área útil do aparelho, à rolagem e ao teclado.

## Onde aparece no código

- `src/Workoutly.Mobile/Resources/Styles/Colors.xaml`: valores da paleta extraídos do Figma.
- `Resources/Styles/Styles.xaml`: estilos reutilizáveis PrimaryButton, SecondaryButton, TextButton, InputBorder, SurfaceCard, PageTitle, Eyebrow e textos auxiliares. Uma mudança nesse arquivo atualiza os componentes que usam o estilo.
- `MauiProgram.cs`: registra os quatro pesos de Inter; os handlers retiram a borda/linha nativa de Entry e Editor para que o Border XAML apresente o contorno do design. Edição, senha, teclado, acessibilidade e HTTP continuam nativos.
- `MainPage.xaml`: acesso com a composição do frame Entrar; cadastro derivado dos mesmos campos; início com saudação real da conta, avatar original e cartões no padrão do frame Início.
- `ExercisesPage.xaml` e `ExercisePage.xaml`: catálogo, busca, detalhes e cadastro seguindo títulos, etiquetas e cartões do frame Minhas fichas. Não havia um frame específico de catálogo nesse arquivo.
- `App.xaml.cs` e estilo de Shell: aparência escura consistente com a identidade, incluindo navegação. As páginas respeitam as áreas de sistema e teclado; o acesso dispensa a barra de título superior.

Os caminhos após o primeiro item são relativos a `src/Workoutly.Mobile`.

## Recursos originais

A marca **6:3** foi exportada do Figma para `Resources/Images/workoutly_logo.png` em seu tamanho original de 42 × 42, sem redesenho. O mesmo arquivo é usado em AppIcon/appicon.png e Splash/splash.png. O avatar **8:4** foi baixado como `Resources/Images/figma_avatar.svg`; suas dimensões originais de 46 × 46 foram preservadas e a tela usa o mesmo tamanho. São recursos locais, sem URLs temporárias no código. O manifesto em design/manifest.json registra origem e hashes.

As imagens de tela em docs/design são referências de comparação, não componentes usados para simular a interface. Não houve incorporação de React/Tailwind: a implementação usa XAML e code-behind, com os controles MAUI já adotados.

## Adaptação ao estágio atual

O protótipo apresenta fichas, treino em andamento e histórico com exemplos de quantidade, carga e sequência. Fichas estão implementadas em PlansPage/PlanPage/PlanExercisePickerPage; execução e consulta básica de sessões estão implementadas em WorkoutPage/WorkoutsPage/WorkoutDetailsPage; a correção usa WorkoutEditPage e os mesmos campos/cartões; indicadores por período e acompanhamento pelo professor continuam no backlog. A tela inicial usa a composição de cartões do design com a conta real e acesso funcional ao catálogo; não exibe os valores fictícios de progresso nem botões sem função para esses recursos futuros. Minhas fichas está acessível no início; o professor abre as fichas pela lista de alunos. Treinos salvos está acessível no início do aluno; o painel de indicadores permanece pendente.

O comando Conexão continua acessível no topo do acesso para configurar o serviço. Cadastro, erros, carregamento, privacidade e estados vazios foram adaptados à paleta; são estados necessários do aplicativo que não aparecem nos frames consultados.

PlansPage usa o frame Minhas fichas (5:4), consultado novamente em 21/10/2026, com dados reais de nome, observações e quantidade de exercícios/séries. Durações e recomendações fictícias do protótipo não são apresentadas. Os formulários e seleção reutilizam os tokens e componentes nativos existentes; não há imagens estáticas nesse frame.

## Como conferir

Execute o ambiente local e o app conforme README.md. Confira acesso, alternância de cadastro, início autenticado, catálogo, busca, filtro, detalhes e criação de exercício. A validação visual exige executar o app: compilar sozinho não mostra se o tema foi aplicado. Evidências e limites em [verificação visual](evidencias/identidade-visual.md).

Referências técnicas: [áreas seguras no .NET MAUI 10](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/safe-area?view=net-maui-10.0) e [customização de controles com handlers](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/handlers/customize). Foram usadas para respeitar barras/teclado e manter os campos nativos dentro dos contornos XAML.

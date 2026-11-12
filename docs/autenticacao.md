# Conta e acesso no Workoutly

Este incremento liga o aplicativo MAUI à API e ao MySQL para cadastro, login, perfil e logout. Exercícios e fichas continuam para a próxima etapa.

## Como funciona

1. O app primeiro permite escolher aluno ou professor e envia nome, e-mail, senha e role para a API no cadastro. Os dados e o papel são validados novamente no servidor.
2. A API armazena a senha com o PasswordHasher do ASP.NET Core Identity: PBKDF2, salt aleatório e 210.000 iterações. Não é um hash simples nem senha em texto.
3. O servidor emite um token aleatório de 32 bytes, válido por sete dias. O MySQL guarda somente seu SHA-256, associado ao usuário e à expiração.
4. O celular guarda token e endereço do serviço no SecureStorage. Somente o endereço do serviço, sem token, também fica nas preferências. A senha não é salva.
5. Ao reabrir, o app lê o token e consulta /auth/me. A API verifica a sessão no banco antes de devolver o perfil. Token inválido, adulterado, expirado ou revogado não autentica.
6. Sair revoga a sessão atual no MySQL e depois remove o token local. Outras sessões do mesmo usuário não são encerradas. Se a rede falhar, a tela informa a falha e permite tentar o logout novamente.

São tokens opacos, não JWT: o conteúdo não carrega dados do usuário e a revogação tem efeito na próxima requisição. Não há token de renovação neste MVP; após sete dias, é preciso entrar novamente.

## Contratos da API

| Método e rota | Entrada | Sucesso | Falhas previstas |
| --- | --- | --- | --- |
| POST /auth/register | name, email, password, role (`student` ou `teacher`) | 201 com accessToken, expiresAt e user | 400 dados/papel inválidos; 409 e-mail existente; 429 limite; 503 banco indisponível. |
| POST /auth/login | email, password | 200 com a mesma estrutura de sessão | 400 formato; 401 e-mail ou senha inválidos; 429 limite; 503 indisponível. |
| GET /auth/me | Authorization: Bearer token | 200 com id, name e email da própria conta | 401 sem sessão válida; 503 indisponível. |
| POST /auth/logout | Authorization: Bearer token | 204 sem corpo | 401 sessão inválida; 503 indisponível. |

Nome: 2–120 caracteres, após remover espaços nas extremidades. E-mail: formato válido, até 254 caracteres, sem distinção de maiúsculas para identificação. Senha de cadastro: 10–128 caracteres, não composta só de espaços; a senha não é aparada ou normalizada. O papel de cadastro é obrigatoriamente `student` ou `teacher`; qualquer outro valor é recusado. E-mails repetidos também são impedidos por restrição única do MySQL.

Login inválido usa uma mensagem genérica. Cadastro e login têm limite de 20 requisições por minuto por IP. Respostas de conta usam Cache-Control: no-store. A API exige HTTPS para /auth fora de Development. Não há confirmação de e-mail, recuperação de senha, MFA ou edição de conta nesta entrega; nenhuma ação dessas é apresentada como pronta.

## Onde está cada parte

- **AuthService:** verifica credenciais e acessa users/user_sessions com SQL parametrizado. Cadastro e criação da primeira sessão são uma transação.
- **SessionAuthenticationHandler:** converte uma sessão válida em identidade autenticada. /auth/me usa essa identidade, não um ID fornecido pelo cliente.
- **002_accounts.sql:** cria users e user_sessions e registra a versão 2 do esquema. O script 001 original foi preservado. O comando --migrate reaplica os scripts idempotentes em ordem.
- **AccountClient no app:** faz HTTP, preserva o formulário em erros e gerencia SecureStorage. Falha do armazenamento seguro não causa fallback para arquivo, Preferences ou banco local.
- **MainPage:** interface XAML com code-behind para cadastro, login e conta, estados de carregamento e mensagens em português.

## Executar neste Mac sem Docker

```sh
python3 scripts/dev-native.py
```

Também é possível abrir Executar-local.command. O helper compila a API, inicia uma instância exclusiva do MySQL nativo, prepara o esquema e serve a API em http://localhost:5080. Os dados persistem em .local/native-mysql/data; os arquivos não são versionados. Ctrl+C encerra os serviços preservando os dados. O socket MySQL é temporário e não há porta MySQL TCP exposta.

A API usa o usuário workoutly, limitado ao próprio banco, com senha do .env e TLS. O helper verifica essa credencial sem imprimi-la nem alterá-la. Não regenere o .env quando já houver dados. Para o ambiente Docker, seguir mysql.md; o modelo agora também exige TLS entre a API e o MySQL.

No simulador iOS, o endereço padrão de Debug é http://localhost:5080/. No emulador Android, http://10.0.2.2:5080/. A tela **Conexão** permite selecionar outro serviço. Trocar de serviço limpa a sessão local e não envia o token ao novo endereço. Em Release, só HTTPS é aceito, inclusive na restauração da sessão. Não há desativação da validação de certificados no cliente HTTP.

HTTP é permitido apenas em builds Debug para desenvolvimento local. A exceção iOS está em um manifesto parcial incluído somente em Debug; no Android, o atributo UsesCleartextTraffic também é condicionado a Debug. O entitlement de Keychain e a assinatura ad hoc se aplicam somente ao simulador iOS; aparelho físico continua exigindo assinatura/provisionamento Apple.

Telefones físicos não usam o localhost do computador. Exigem configurar um endereço HTTPS acessível e certificado confiável. Esse cenário não foi validado nesta etapa.

## Verificações e reprodução

```sh
python3 scripts/test-native-mysql.py
```

O teste cria bancos e usuários restritos aleatórios em um MySQL temporário. Verifica dados inválidos, cadastro, login incorreto/correto, e-mail duplicado, perfil, logout, revogação, token adulterado, expiração simulada pelo relógio do servidor, duas contas e restauração por outra instância da API. O teste não simula persistência com SQLite.

No iPhone Simulator foram exercitados cadastro com dados fictícios, reabertura com sessão restaurada, logout, senha incorreta, novo login e falha de conexão com campos preservados. Detalhes e imagens em evidencias/autenticacao.md. Android foi compilado; sua execução e os aparelhos físicos continuam sem validação nesta etapa.

Referências dos componentes: [PasswordHasherOptions](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0) e [SecureStorage do MAUI](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/secure-storage?view=net-maui-10.0).

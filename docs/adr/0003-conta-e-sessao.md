# Decisão 0003 Conta com sessão revogável

Data: 20/10/2026. Estado: adotada para AUTH-01.

Usar o PasswordHasher do ASP.NET Core Identity para proteger senhas, sem introduzir um ORM ou reimplementar criptografia. Conta identificada por ID aleatório e e-mail normalizado, com unicidade no MySQL.

Adotar tokens opacos aleatórios, com hash no MySQL e expiração de sete dias, em vez de tokens autocontidos. Consultar a sessão a cada requisição permite revogar imediatamente o token no logout. Não há refresh token nem recuperação de senha nesta etapa.

Guardar o token somente no SecureStorage do MAUI, vinculado ao endereço do serviço. Preferences guarda somente a configuração do endereço. Falhas de armazenamento seguro não autorizam persistência insegura. Falhas de rede preservam os campos e não simulam sucesso do logout.

Exigir HTTPS fora de desenvolvimento. Exceções HTTP do app ficam limitadas a Debug; Release rejeita HTTP também ao restaurar a sessão. Não ignorar validação de certificados. MySQL do ambiente local usa TLS e conta restrita; helper nativo preserva os dados em .local/ sem alterar serviços MySQL existentes.

O isolamento da identidade foi verificado em /auth/me. A autorização de fichas, exercícios e sessões precisa ser implementada por proprietário quando esses recursos existirem; AUTH-02 permanece parcial.

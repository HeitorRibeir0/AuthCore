# AuthCore

API de autenticação e autorização construída com ASP.NET Core 8, JWT, refresh token rotation e observabilidade estruturada.

---

## O que é o projeto

AuthCore é um sistema de auth production-ready desenvolvido como portfólio. Cobre o ciclo completo de autenticação: registro, login, renovação de sessão, logout, reset de senha e controle de acesso por role — com foco em segurança real, não em segurança aparente.

---

## Arquitetura

```
src/AuthCore.API/
├── Controllers/         → Endpoints HTTP
├── Application/
│   ├── Interfaces/      → Contratos de serviço
│   ├── Services/        → Lógica de negócio
│   └── Validators/      → FluentValidation
├── Entities/            → User, RefreshToken, PasswordResetToken
├── Enums/               → Role (User | Admin)
├── Exceptions/          → Exceções de domínio tipadas
├── Infrastructure/
│   ├── Data/            → AppDbContext + Migrations (EF Core)
│   └── Repositories/
├── Middleware/          → ExceptionHandler, RequestId, SecurityHeaders
├── Extensions/          → RateLimiting
└── appsettings.json
```

**Stack:** ASP.NET Core 8 · EF Core 8 · PostgreSQL · Serilog · FluentValidation · BCrypt · JWT Bearer

---

## Fluxo de autenticação

```
POST /auth/register
  → valida input (FluentValidation)
  → verifica email único
  → BCrypt.HashPassword
  → salva User
  → gera JWT (15min) + Refresh Token (7d)
  → Refresh Token: hash SHA-256 salvo no banco, raw no cookie httpOnly
  ← 201 { accessToken }

POST /auth/login
  → busca user por email
  → BCrypt.Verify (mesmo caminho se não encontrar → evita enumeração)
  → gera JWT + Refresh Token
  ← 200 { accessToken } + cookie refreshToken

POST /auth/refresh
  → lê cookie refreshToken
  → SHA-256 hash → busca no banco
  → se RevokedAt != null → reuse detection → revoga todos os tokens do usuário
  → revoga token atual → gera novo par
  ← 200 { accessToken } + novo cookie

POST /auth/logout
  → revoga refresh token no banco
  → limpa cookie
  ← 204

POST /auth/forgot-password
  → gera reset token (SHA-256, 15min, uso único)
  → loga token no console (produção: enviar por e-mail)
  → retorna 200 genérico independente do e-mail existir
  ← 200 { message }

POST /auth/reset-password
  → valida token (hash, expiração, UsedAt == null)
  → marca UsedAt (invalida para reuso)
  → troca senha com BCrypt
  → revoga todas as sessões ativas
  ← 204
```

---

## Segurança

| Mecanismo | Detalhe |
|---|---|
| Senhas | BCrypt com work factor padrão |
| Refresh tokens | Armazenados como hash SHA-256 — token raw nunca fica no banco |
| Reset tokens | Hash SHA-256, expiração 15min, uso único via `UsedAt` |
| Cookie | httpOnly, Secure (produção), SameSite=Strict |
| JWT | HS256, expiração 15min, claims: sub, email, role, jti |
| Enumeração de usuário | Login retorna mensagem genérica para e-mail inválido e senha errada |
| Reuse detection | Refresh token já revogado → revoga todas as sessões do usuário |
| Reset de senha | Revoga todas as sessões — senha comprometida não mantém sessões abertas |
| Security headers | X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, Referrer-Policy |

---

## Device tracking

Cada refresh token armazena `UserAgent` e `IpAddress` no momento da criação, permitindo auditoria de sessões ativas por dispositivo/localização.

---

## Observabilidade

- **Serilog** com output estruturado e template configurável via `appsettings.json`
- **RequestId (correlation ID):** gerado ou lido do header `X-Request-ID`, injetado no `LogContext` e retornado em todos os responses
- **Logs de segurança:**
  - Falha de login: `Warning` com email e IP
  - Refresh token reutilizado: `Warning` com UserId
  - Reset de senha: `Information` com UserId e número de sessões revogadas

---

## Endpoints

| Método | Rota | Auth | Rate Limit |
|---|---|---|---|
| POST | /api/v1/auth/register | — | 3/min por IP |
| POST | /api/v1/auth/login | — | 5/min por IP |
| POST | /api/v1/auth/refresh | — | — |
| POST | /api/v1/auth/logout | — | — |
| POST | /api/v1/auth/register/admin | Admin | 3/min por IP |
| POST | /api/v1/auth/forgot-password | — | 3/min por IP |
| POST | /api/v1/auth/reset-password | — | — |
| GET | /api/v1/users | Admin | 60/min por usuário |
| GET | /api/v1/users/me | User/Admin | 60/min por usuário |
| PATCH | /api/v1/users/me/password | User/Admin | 60/min por usuário |
| GET | /health | — | — |

---

## Como rodar

**Pré-requisito:** Docker instalado.

```bash
# Clone o repositório
git clone <repo-url>
cd AuthCore

# Copie o arquivo de variáveis
cp .env.example .env

# Suba tudo
docker-compose up --build
```

API disponível em `http://localhost:8080`.  
Health check: `http://localhost:8080/health`.

---

## Testes

```bash
# Unitários (sem dependências externas)
dotnet test --filter "FullyQualifiedName~Unit"

# Integração (requer Docker para Testcontainers)
dotnet test --filter "FullyQualifiedName~Integration"

# Todos
dotnet test
```

**Cobertura dos testes unitários (12):**
- Register: email duplicado, idempotência, role admin
- Login: credenciais corretas, senha errada, e-mail inexistente (mesmo erro que senha errada)
- Refresh: token válido com rotation, token expirado, token reutilizado revoga tudo, token inválido
- Logout: token revogado no banco

**Integração (2, banco real via Testcontainers):**
- Fluxo login completo: register → login → AccessToken + cookie
- Fluxo refresh: login → refresh → novo AccessToken

---

## Decisões técnicas

**Por que refresh token rotation?**  
Token estático é um segredo permanente — uma vez vazado, o atacante tem acesso indefinido. Com rotation, cada uso gera um novo token e invalida o anterior. Um token reutilizado (reuse detection) indica possível roubo e revoga toda a sessão do usuário.

**Por que hash SHA-256 do refresh token?**  
O banco de dados é uma superfície de ataque. Se um dump vazar, tokens em plain text viram acesso imediato. O hash garante que o token raw (que transita apenas na memória e no cookie) nunca é armazenado — mesma lógica que se aplica a senhas com BCrypt.

**Por que mensagem genérica no login?**  
Retornar "e-mail não encontrado" vs "senha incorreta" permite enumeração de usuários — um atacante descobre quais e-mails estão cadastrados sem autenticar. A mensagem genérica elimina essa diferença.

**Por que rate limiting por IP + usuário?**  
Rate limit por IP protege endpoints públicos (login, register) contra ataques de força bruta e credential stuffing. Para rotas autenticadas, o limite por usuário evita abuso mesmo em redes compartilhadas (múltiplos usuários no mesmo IP).

**Por que armazenar UserAgent e IpAddress?**  
Habilita auditoria de sessões: de onde e de qual dispositivo cada token foi gerado. Em sistemas com detecção de anomalia, uma sessão vinda de IP/UserAgent diferente do original pode ser sinalizada ou invalidada automaticamente.

**Por que `UsedAt` em vez de deletar o reset token?**  
Deletar remove o histórico. Com `UsedAt`, é possível auditar quando e quantas vezes um reset foi solicitado e consumido — útil para investigação de incidentes. Tokens expirados não usados podem ser limpos por job agendado sem perder rastreabilidade.

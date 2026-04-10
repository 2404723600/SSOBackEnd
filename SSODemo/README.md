# SSO Demo with Keycloak

这是一个使用 ASP.NET Core MVC 模板实现的单点登录 (SSO) 后端服务，集成 Keycloak 作为身份认证提供者。

## 项目结构

```
SSODemo/
├── Controllers/
│   └── HomeController.cs      # 主控制器，处理登录、登出和用户信息展示
├── Models/
│   └── UserInfoViewModel.cs   # 用户信息视图模型
├── Views/
│   ├── Home/
│   │   ├── Index.cshtml       # 主页（显示用户信息）
│   │   ├── Profile.cshtml     # 用户资料页
│   │   └── Error.cshtml       # 错误页面
│   ├── Shared/
│   │   └── _Layout.cshtml     # 共享布局
│   ├── _ViewImports.cshtml    # Razor 视图导入
│   └── _ViewStart.cshtml      # 视图起始配置
├── Properties/
├── appsettings.json           # 应用配置文件（包含 Keycloak 配置）
├── appsettings.Development.json
├── Program.cs                 # 程序入口和 SSO 配置
└── SSODemo.csproj             # 项目文件
```

## Keycloak 配置说明

在 `appsettings.json` 中配置 Keycloak 连接参数：

```json
"Keycloak": {
  "Authority": "https://192.168.41.225:8443/realms/beam",
  "Audience": "webapi1",
  "ValidateAudience": true,
  "ValidAudiences": [ "webapi1", "account" ],
  "RequireHttpsMetadata": true,
  "AllowInsecureBackchannel": true,
  "ValidIssuer": "https://192.168.41.225:8443/realms/beam"
}
```

### 配置项说明

| 配置项 | 说明 |
|--------|------|
| Authority | Keycloak Realm 的 URL |
| Audience | 预期的令牌受众（客户端 ID） |
| ValidateAudience | 是否验证令牌的受众 |
| ValidAudiences | 允许的有效受众列表 |
| RequireHttpsMetadata | 是否要求 HTTPS 元数据 |
| AllowInsecureBackchannel | 允许不安全的反向通道（开发环境使用自签名证书时设为 true） |
| ValidIssuer | 有效的发行者 URL（fallback） |

## Keycloak 服务端配置步骤

### 1. 创建 Realm
- 登录 Keycloak 管理控制台
- 创建名为 `beam` 的 Realm（或使用现有 Realm）

### 2. 创建客户端
- 在 Realm 中创建新客户端
- **Client ID**: `webapi1`
- **Client Protocol**: `openid-connect`
- **Access Type**: `confidential`（如果需要 client secret）或 `public`
- **Valid Redirect URIs**: `https://localhost:7000/*`（根据实际运行端口配置）
- **Web Origins**: `+`（允许所有 CORS）或指定具体域名

### 3. 配置客户端作用域
确保客户端有以下 Mapper：
- `audience` mapper，包含 `webapi1` 和 `account`

### 4. 创建测试用户
- 在 Users 中创建测试用户
- 设置密码
- 分配角色（如需要）

## 运行项目

### 前提条件
- .NET 8.0 SDK
- Keycloak 服务器已启动并可访问

### 运行命令

```bash
cd SSODemo
dotnet restore
dotnet run
```

默认情况下，应用将在 `https://localhost:7000` 或 `http://localhost:5000` 运行。

### 配置 HTTPS（开发环境）

如果 Keycloak 使用自签名证书，确保 `AllowInsecureBackchannel` 设置为 `true`。

## 功能说明

### 认证流程
1. 用户访问受保护的页面（如 `/Home/Index`）
2. 如果未认证，自动重定向到 Keycloak 登录页面
3. 用户在 Keycloak 输入凭据
4. Keycloak 验证后重定向回应用，携带授权码
5. 应用使用授权码换取 tokens
6. 创建本地 Cookie 会话

### 可用端点

| 端点 | 描述 | 认证要求 |
|------|------|----------|
| `/` | 欢迎页面 | 否 |
| `/Home/Index` | 用户仪表板（显示用户信息和 claims） | 是 |
| `/Home/Profile` | 用户资料页面 | 是 |
| `/Home/Login` | 触发登录流程 | 否 |
| `/Home/Logout` | 登出当前用户 | 否 |
| `/Home/Error` | 错误页面 | 否 |

## 自定义配置

### 修改 Client ID
在 `Program.cs` 中修改：
```csharp
options.ClientId = "your-client-id";
options.ClientSecret = "your-client-secret"; // 如果是 confidential 客户端
```

### 添加额外的 Claims
在 `Program.cs` 的 `OnTokenValidated` 事件中添加自定义逻辑。

### 修改 Cookie 设置
在 `Program.cs` 的 `.AddCookie()` 配置中调整：
- `ExpireTimeSpan`: Cookie 过期时间
- `SlidingExpiration`: 是否滑动过期
- `Cookie.Name`: Cookie 名称

## 故障排除

### 常见问题

1. **SSL/TLS 证书错误**
   - 确保 `AllowInsecureBackchannel = true`（仅开发环境）
   - 或将 Keycloak 证书添加到信任存储

2. **重定向 URI 不匹配**
   - 检查 Keycloak 客户端配置中的 Valid Redirect URIs
   - 确保与实际运行地址匹配

3. **Audience 验证失败**
   - 确认 Keycloak 客户端配置中包含正确的 audience
   - 检查 `ValidAudiences` 配置

4. **CORS 问题**
   - 在 Keycloak 客户端配置中设置正确的 Web Origins

## 安全建议

生产环境部署时：
- ❌ 不要设置 `AllowInsecureBackchannel = true`
- ✅ 使用有效的 SSL 证书
- ✅ 配置适当的 Cookie 安全策略
- ✅ 启用 CSRF 保护
- ✅ 限制 Session 超时时间
- ✅ 监控认证日志

## 技术栈

- ASP.NET Core 8.0
- OpenID Connect (OIDC)
- Cookie Authentication
- Bootstrap 5 (UI)

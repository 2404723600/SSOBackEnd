# Keycloak 登出配置详细指南

## 问题描述
登出时出现错误：`Missing parameters: id_token_hint`

这是因为 Keycloak 要求在前端发起登出请求时必须提供 `id_token_hint` 参数，用于验证登出请求的合法性。

## 解决方案

### 一、代码层面修改（已完成）

1. **保存 id_token**
   - 在 `Program.cs` 中设置 `options.SaveTokens = true`
   - 在 `OnAuthorizationCodeReceived` 事件中捕获 `id_token` 并保存到用户 Claims 中

2. **登出时传递 id_token_hint**
   - 在 `HomeController.Logout()` 方法中从 Claims 读取 `id_token`
   - 构造包含 `id_token_hint` 和 `post_logout_redirect_uri` 的 Keycloak 登出 URL

### 二、Keycloak 服务端配置（必须手动完成）

#### 步骤 1：登录 Keycloak 管理控制台
```
https://192.168.41.225:8443/admin
```

#### 步骤 2：选择 Realm
- 选择 `beam` Realm（或您使用的 Realm 名称）

#### 步骤 3：找到客户端
- 点击左侧菜单 **Clients**
- 找到并点击 `webapi1` 客户端

#### 步骤 4：配置 Post Logout Redirect URIs

**Keycloak 17+ (Quarkus 版本):**
1. 在客户端详情页，确保在 **Settings** 标签页
2. 找到 **Post logout redirect URIs** 字段
3. 添加您的应用登出后重定向地址：
   ```
   https://localhost:7000/*
   ```
   或者精确匹配：
   ```
   https://localhost:7000/
   https://localhost:7000/Home/Index
   ```
4. 点击 **Save** 保存

**Keycloak 旧版本 (WildFly):**
1. 在客户端详情页的 **Settings** 标签页
2. 找到 **Valid Redirect URIs** 字段
3. 确保包含登出回调地址：
   ```
   https://localhost:7000/*
   ```
4. 某些版本有独立的 **Post Logout Redirect URI** 字段，同样配置
5. 点击 **Save** 保存

#### 步骤 5：验证其他必要配置

确保以下配置正确：

| 配置项 | 推荐值 | 说明 |
|--------|--------|------|
| Client ID | `webapi1` | 与代码中 `options.ClientId` 一致 |
| Client Protocol | `openid-connect` | 必须使用 OIDC 协议 |
| Access Type | `confidential` 或 `public` | 根据是否需要 client secret |
| Valid Redirect URIs | `https://localhost:7000/*` | 登录回调地址 |
| Web Origins | `+` 或 `https://localhost:7000` | CORS 配置 |
| Post Logout Redirect URIs | `https://localhost:7000/*` | **登出回调地址（关键）** |

#### 步骤 6：配置 Fine-Grained OpenID Connect Configuration（可选但推荐）

1. 在客户端详情页，点击 **Fine grain openID connect configuration** 展开
2. 确保以下设置：
   - **User Info Signed Response Algorithm**: `RS256`
   - **Request Object Signature Algorithm**: `RS256`
   - **Token Endpoint Authentication Method**: `client-secret-post` (如果是 confidential 客户端)

### 三、测试步骤

1. **清除浏览器缓存和 Cookie**
   ```
   - Chrome: Ctrl+Shift+Del → 清除 Cookie 和缓存
   - 或使用无痕模式 (Incognito)
   ```

2. **重新登录应用**
   ```
   - 访问 https://localhost:7000/Home/Index
   - 会自动重定向到 Keycloak 登录页面
   - 输入用户名和密码登录
   - 成功登录后应显示用户信息页面
   ```

3. **检查 id_token 是否保存成功**
   - 查看应用日志，应该看到类似：
     ```
     IdToken captured from TokenEndpointResponse: eyJhbGciOiJSUzI1NiIsInR...
     IdToken added to user claims
     ```
   - 在用户信息页面的 Claims 列表中，应该能看到 `id_token` 字段

4. **测试登出功能**
   - 点击 "Logout" 按钮
   - 应该重定向到 Keycloak 登出端点，URL 类似：
     ```
     https://192.168.41.225:8443/realms/beam/protocol/openid-connect/logout?post_logout_redirect_uri=https%3A%2F%2Flocalhost%3A7000%2FHome%2FIndex&id_token_hint=eyJhbGciOiJSUzI1NiIsInR...
     ```
   - Keycloak 处理登出后，应自动重定向回应用首页
   - 再次访问受保护页面时，应要求重新登录

### 四、常见问题排查

#### 问题 1: 仍然提示 "Missing parameters: id_token_hint"

**可能原因：**
- Keycloak 的 Post Logout Redirect URIs 未配置或配置错误
- id_token 未正确保存到 Claims

**解决方法：**
1. 确认 Keycloak 已配置 Post Logout Redirect URIs
2. 检查应用日志，确认看到 "IdToken captured from TokenEndpointResponse"
3. 完全清除浏览器 Cookie 后重新登录

#### 问题 2: 登出后没有重定向回应用

**可能原因：**
- post_logout_redirect_uri 与 Keycloak 配置不匹配
- Keycloak 版本不支持自动重定向

**解决方法：**
1. 检查 post_logout_redirect_uri 的值是否与 Keycloak 中配置的完全一致（包括协议、端口、路径）
2. 尝试使用通配符 `https://localhost:7000/*`
3. 对于旧版 Keycloak，可能需要在登出 URL 中显式指定 `redirect_uri` 参数

#### 问题 3: SSL 证书错误

**可能原因：**
- Keycloak 使用自签名证书
- 开发环境未配置证书信任

**解决方法：**
1. 确保 `AllowInsecureBackchannel = true`（仅开发环境）
2. 或在代码中配置证书验证回调：
   ```csharp
   options.BackchannelHttpHandler = new HttpClientHandler
   {
       ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
   };
   ```

### 五、完整登出流程说明

```
用户点击 Logout
    ↓
HomeController.Logout() 执行
    ↓
从 Claims 中读取 id_token
    ↓
清除本地 Cookie (HttpContext.SignOutAsync)
    ↓
构造 Keycloak 登出 URL:
{authority}/protocol/openid-connect/logout
  ?post_logout_redirect_uri={encoded_redirect_uri}
  &id_token_hint={encoded_id_token}
    ↓
重定向到 Keycloak 登出端点
    ↓
Keycloak 验证 id_token_hint
    ↓
Keycloak 清除服务器端会话
    ↓
Keycloak 重定向到 post_logout_redirect_uri
    ↓
用户回到应用首页（未登录状态）
```

## 参考链接

- [Keycloak 官方文档 - Logout](https://www.keycloak.org/docs/latest/securing_apps/#logout)
- [OpenID Connect Session Management](https://openid.net/specs/openid-connect-session-1_0.html)
- [ASP.NET Core OpenID Connect 文档](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/openidconnect)

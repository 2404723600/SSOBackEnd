namespace SSODemo.Models;

public class UserInfoViewModel
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? UserId { get; set; }
    public IEnumerable<string> Roles { get; set; } = new List<string>();
    public IDictionary<string, string> Claims { get; set; } = new Dictionary<string, string>();
}

using Microsoft.AspNetCore.Authorization;

namespace PhotoRental.Attributes
{
    public class AdminAuthorizeAttribute : AuthorizeAttribute
    {
        public AdminAuthorizeAttribute()
        {
            Roles = "Admin";
        }
    }
}
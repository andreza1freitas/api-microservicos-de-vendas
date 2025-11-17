using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Auth.API.Models
{
    public class LoginModel
    {
        [Required(ErrorMessage = "O nome de usuário é obrigatório.")]
        public string? Username { get; set; }
        
        [Required(ErrorMessage = "A senha é obrigatória.")]
        public string? Password { get; set; }
    }
}
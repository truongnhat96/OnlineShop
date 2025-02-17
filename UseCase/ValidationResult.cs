using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase
{
    public enum ValidationResult
    {
        UserExists,
        UsernameNotValid,
        EmailExists,
        PasswordNotMatch,
        PasswordTooShort,
        Success
    }
}

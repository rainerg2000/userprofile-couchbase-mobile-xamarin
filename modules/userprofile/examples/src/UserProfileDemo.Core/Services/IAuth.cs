using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace UserProfileDemo.Core.Services
{
  public interface IAuth
  {
      Task<string> GetSgSessionToken(Uri baseUri);
      Task<string> GetJwt();
  }
}

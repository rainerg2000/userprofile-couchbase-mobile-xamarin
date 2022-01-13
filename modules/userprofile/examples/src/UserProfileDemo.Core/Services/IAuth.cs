using System;
using System.Threading.Tasks;

namespace UserProfileDemo.Core.Services
{
  public interface IAuth
  {
      Task<string> GetSgSessionToken(Uri baseUri);
  }
}

using Crypter.Core.Interfaces;
using System.Threading.Tasks;

namespace Crypter.Console.Jobs
{
   class DeleteUser
   {
      private readonly IUserService UserService;

      public DeleteUser(IUserService userService)
      {
         UserService = userService;
      }

      /// <summary>
      /// Deletes user by username.
      /// Returns false if username does not exist.
      /// </summary>
      /// <param name="username"></param>
      /// <returns></returns>
      public async Task<bool> RunAsync(string username)
      {
         var user = await UserService.ReadAsync(username);
         if (user == default)
         {
            return false;
         }
         await UserService.DeleteAsync(user.Id);
         return true;
      }
   }
}

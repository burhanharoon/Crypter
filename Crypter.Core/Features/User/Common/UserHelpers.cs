﻿/*
 * Copyright (C) 2022 Crypter File Transfer
 * 
 * This file is part of the Crypter file transfer project.
 * 
 * Crypter is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * The Crypter source code is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 * 
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 * You can be released from the requirements of the aforementioned license
 * by purchasing a commercial license. Buying such a license is mandatory
 * as soon as you develop commercial activities involving the Crypter source
 * code without disclosing the source code of your own applications.
 * 
 * Contact the current copyright holder to discuss commercial license options.
 */

using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace Crypter.Core.Features.User.Common
{
   internal static class UserHelpers
   {
      public static async Task<bool> IsUsernameAvailableAsync(this DbSet<Models.User> userContext, string username, CancellationToken cancellationToken)
      {
         string usernameLowercase = username.ToLower();
         return !await userContext
            .AnyAsync(x => x.Username.ToLower() == usernameLowercase, cancellationToken);
      }

      public static async Task<bool> IsEmailAddressAvailableAsync(this DbSet<Models.User> userContext, string email, CancellationToken cancellationToken)
      {
         string emailLowercase = email.ToLower();
         return !await userContext
            .AnyAsync(x => x.Email.ToLower() == emailLowercase, cancellationToken);
      }
   }
}
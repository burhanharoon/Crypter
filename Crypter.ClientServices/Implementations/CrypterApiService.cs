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

using Crypter.ClientServices.Interfaces;
using Crypter.Common.Monads;
using Crypter.Contracts.Common;
using Crypter.Contracts.Common.Enum;
using Crypter.Contracts.Features.Authentication.Login;
using Crypter.Contracts.Features.Authentication.Logout;
using Crypter.Contracts.Features.Authentication.Refresh;
using Crypter.Contracts.Features.Metrics.Disk;
using Crypter.Contracts.Features.Transfer.DownloadCiphertext;
using Crypter.Contracts.Features.Transfer.DownloadPreview;
using Crypter.Contracts.Features.Transfer.DownloadSignature;
using Crypter.Contracts.Features.Transfer.Upload;
using Crypter.Contracts.Features.User.GetPublicProfile;
using Crypter.Contracts.Features.User.GetReceivedTransfers;
using Crypter.Contracts.Features.User.GetSentTransfers;
using Crypter.Contracts.Features.User.GetSettings;
using Crypter.Contracts.Features.User.Register;
using Crypter.Contracts.Features.User.Search;
using Crypter.Contracts.Features.User.UpdateContactInfo;
using Crypter.Contracts.Features.User.UpdateKeys;
using Crypter.Contracts.Features.User.UpdateNotificationSettings;
using Crypter.Contracts.Features.User.UpdatePrivacySettings;
using Crypter.Contracts.Features.User.UpdateProfile;
using Crypter.Contracts.Features.User.VerifyEmailAddress;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Crypter.ClientServices.Implementations
{
   internal static class ApiExtensions
   {
      public static Either<TErrorCode, TResponse> ExtractErrorCode<TErrorCode, TResponse>(this Either<ErrorResponse, TResponse> response)
         where TErrorCode : Enum
      {
         return response.Match<Either<TErrorCode, TResponse>>(
            left => (TErrorCode)(object)left.ErrorCode,
            right => right);
      }
   }

   public class CrypterApiService : ICrypterApiService
   {
      private readonly IHttpService _httpService;
      private readonly ITokenRepository _tokenRepository;

      private readonly string _baseAuthenticationUrl;
      private readonly string _baseMetricsUrl;
      private readonly string _baseUserUrl;
      private readonly string _baseTransferUrl;

      public CrypterApiService(IHttpService httpService, ITokenRepository tokenRepository, IClientApiSettings clientApiSettings)
      {
         _httpService = httpService;
         _tokenRepository = tokenRepository;
         _baseAuthenticationUrl = $"{clientApiSettings.ApiBaseUrl}/authentication";
         _baseMetricsUrl = $"{clientApiSettings.ApiBaseUrl}/metrics";
         _baseUserUrl = $"{clientApiSettings.ApiBaseUrl}/user";
         _baseTransferUrl = $"{clientApiSettings.ApiBaseUrl}/transfer";
      }

      private async Task<string> GetAuthenticationTokenAsync()
         => await _tokenRepository.GetAuthenticationTokenAsync();

      private async Task<string> GetRefreshTokenAsync()
         => await _tokenRepository.GetRefreshTokenAsync();

      private async Task<(HttpStatusCode httpStatus, TResponse response)> UseAuthenticationMiddleware<TResponse>(Func<string, Task<(HttpStatusCode httpStatus, TResponse response)>> request)
      {
         async Task<(HttpStatusCode httpStatus, TResponse response)> MakeRequestAsync()
         {
            string authenticationToken = await GetAuthenticationTokenAsync();
            return await request(authenticationToken);
         }

         var initialAttempt = await MakeRequestAsync();
         if (initialAttempt.httpStatus == HttpStatusCode.Unauthorized)
         {
            var refreshResponse = await RefreshAsync();
            await refreshResponse.DoRightAsync(async x =>
            {
               await _tokenRepository.StoreAuthenticationTokenAsync(x.AuthenticationToken);
               await _tokenRepository.StoreRefreshTokenAsync(x.RefreshToken);
            });

            if (refreshResponse.IsRight)
            {
               return await MakeRequestAsync();
            }
         }

         return initialAttempt;
      }

      public async Task<Either<LoginError, LoginResponse>> LoginAsync(LoginRequest loginRequest)
      {
         string url = $"{_baseAuthenticationUrl}/login";
         var (_, response) = await _httpService.PostAsync<LoginRequest, LoginResponse>(url, loginRequest);

         return response.ExtractErrorCode<LoginError, LoginResponse>();
      }

      public async Task<Either<RefreshError, RefreshResponse>> RefreshAsync()
      {
         string refreshToken = await GetRefreshTokenAsync();

         string url = $"{_baseAuthenticationUrl}/refresh";
         var (_, response) = await _httpService.GetAsync<RefreshResponse>(url, refreshToken);

         return response.ExtractErrorCode<RefreshError, RefreshResponse>();
      }

      public async Task<Either<LogoutError, LogoutResponse>> LogoutAsync(LogoutRequest logoutRequest)
      {
         string refreshToken = await GetRefreshTokenAsync();

         string url = $"{_baseAuthenticationUrl}/logout";
         var (_, response) = await _httpService.PostAsync<LogoutRequest, LogoutResponse>(url, logoutRequest, refreshToken);

         return response.ExtractErrorCode<LogoutError, LogoutResponse>();
      }

      public async Task<Either<DummyError, DiskMetricsResponse>> GetDiskMetricsAsync()
      {
         string url = $"{_baseMetricsUrl}/disk";
         var (_, response) = await _httpService.GetAsync<DiskMetricsResponse>(url);

         return response.ExtractErrorCode<DummyError, DiskMetricsResponse>();
      }

      public async Task<Either<UserRegisterError, UserRegisterResponse>> RegisterUserAsync(UserRegisterRequest registerRequest)
      {
         string url = $"{_baseUserUrl}/register";
         var (_, response) = await _httpService.PostAsync<UserRegisterRequest, UserRegisterResponse>(url, registerRequest);

         return response.ExtractErrorCode<UserRegisterError, UserRegisterResponse>();
      }

      public async Task<Either<GetUserPublicProfileError, GetUserPublicProfileResponse>> GetUserPublicProfileAsync(string username, bool withAuthentication)
      {
         string url = $"{_baseUserUrl}/profile/{username}";
         var (_, response) = withAuthentication
            ? await UseAuthenticationMiddleware(async (token) => await _httpService.GetAsync<GetUserPublicProfileResponse>(url, token))
            : await _httpService.GetAsync<GetUserPublicProfileResponse>(url);

         return response.ExtractErrorCode<GetUserPublicProfileError, GetUserPublicProfileResponse>();
      }

      public async Task<Either<DummyError, UserSettingsResponse>> GetUserSettingsAsync()
      {
         string url = $"{_baseUserUrl}/settings";
         var (_, response) = await UseAuthenticationMiddleware(async (token) => await _httpService.GetAsync<UserSettingsResponse>(url, token));

         return response.ExtractErrorCode<DummyError, UserSettingsResponse>();
      }

      public async Task<Either<UpdateProfileError, UpdateProfileResponse>> UpdateUserProfileInfoAsync(UpdateProfileRequest request)
      {
         string url = $"{_baseUserUrl}/settings/profile";
         var (_, response) = await UseAuthenticationMiddleware(async (token) => await _httpService.PostAsync<UpdateProfileRequest, UpdateProfileResponse>(url, request, token));

         return response.ExtractErrorCode<UpdateProfileError, UpdateProfileResponse>();
      }

      public async Task<Either<UpdateContactInfoError, UpdateContactInfoResponse>> UpdateUserContactInfoAsync(UpdateContactInfoRequest request)
      {
         string url = $"{_baseUserUrl}/settings/contact";
         var (_, response) = await UseAuthenticationMiddleware(async (token) => await _httpService.PostAsync<UpdateContactInfoRequest, UpdateContactInfoResponse>(url, request, token));

         return response.ExtractErrorCode<UpdateContactInfoError, UpdateContactInfoResponse>();
      }

      public async Task<Either<UpdatePrivacySettingsError, UpdatePrivacySettingsResponse>> UpdateUserPrivacyAsync(UpdatePrivacySettingsRequest request)
      {
         string url = $"{_baseUserUrl}/settings/privacy";
         var (_, response) = await UseAuthenticationMiddleware(async (token) => await _httpService.PostAsync<UpdatePrivacySettingsRequest, UpdatePrivacySettingsResponse>(url, request, token));

         return response.ExtractErrorCode<UpdatePrivacySettingsError, UpdatePrivacySettingsResponse>();
      }

      public async Task<Either<UpdateNotificationSettingsError, UpdateNotificationSettingsResponse>> UpdateUserNotificationAsync(UpdateNotificationSettingsRequest request)
      {
         string url = $"{_baseUserUrl}/settings/notification";
         var (_, response) = await UseAuthenticationMiddleware(async (token) => await _httpService.PostAsync<UpdateNotificationSettingsRequest, UpdateNotificationSettingsResponse>(url, request, token));

         return response.ExtractErrorCode<UpdateNotificationSettingsError, UpdateNotificationSettingsResponse>();
      }

      public async Task<Either<UpdateKeysError, UpdateKeysResponse>> InsertUserX25519KeysAsync(UpdateKeysRequest request)
      {
         string url = $"{_baseUserUrl}/settings/keys/x25519";
         var (_, response) = await UseAuthenticationMiddleware(async (token) => await _httpService.PostAsync<UpdateKeysRequest, UpdateKeysResponse>(url, request, token));

         return response.ExtractErrorCode<UpdateKeysError, UpdateKeysResponse>();
      }

      public async Task<Either<UpdateKeysError, UpdateKeysResponse>> InsertUserEd25519KeysAsync(UpdateKeysRequest request)
      {
         string url = $"{_baseUserUrl}/settings/keys/ed25519";
         var (_, response) = await UseAuthenticationMiddleware(async (token) => await _httpService.PostAsync<UpdateKeysRequest, UpdateKeysResponse>(url, request, token));

         return response.ExtractErrorCode<UpdateKeysError, UpdateKeysResponse>();
      }

      public async Task<Either<DummyError, UserSentMessagesResponse>> GetUserSentMessagesAsync()
      {
         string url = $"{_baseUserUrl}/sent/messages";
         var (_, response) = await UseAuthenticationMiddleware(async (token) => await _httpService.GetAsync<UserSentMessagesResponse>(url, token));

         return response.ExtractErrorCode<DummyError, UserSentMessagesResponse>();
      }

      public async Task<Either<DummyError, UserSentFilesResponse>> GetUserSentFilesAsync()
      {
         string url = $"{_baseUserUrl}/sent/files";
         var (_, response) = await UseAuthenticationMiddleware(async (token) => await _httpService.GetAsync<UserSentFilesResponse>(url, token));

         return response.ExtractErrorCode<DummyError, UserSentFilesResponse>();
      }

      public async Task<Either<DummyError, UserReceivedMessagesResponse>> GetUserReceivedMessagesAsync()
      {
         string url = $"{_baseUserUrl}/received/messages";
         var (_, response) = await UseAuthenticationMiddleware(async (token) => await _httpService.GetAsync<UserReceivedMessagesResponse>(url, token));

         return response.ExtractErrorCode<DummyError, UserReceivedMessagesResponse>();
      }

      public async Task<Either<DummyError, UserReceivedFilesResponse>> GetUserReceivedFilesAsync()
      {
         string url = $"{_baseUserUrl}/received/files";
         var (_, response) = await UseAuthenticationMiddleware(async (token) => await _httpService.GetAsync<UserReceivedFilesResponse>(url, token));

         return response.ExtractErrorCode<DummyError, UserReceivedFilesResponse>();
      }

      public async Task<Either<DummyError, UserSearchResponse>> GetUserSearchResultsAsync(UserSearchParameters searchInfo)
      {
         StringBuilder urlBuilder = new($"{_baseUserUrl}/search");
         urlBuilder.Append($"?value={searchInfo.Keyword}");
         urlBuilder.Append($"&index={searchInfo.Index}");
         urlBuilder.Append($"&count={searchInfo.Count}");
         string url = urlBuilder.ToString();

         var (_, response) = await UseAuthenticationMiddleware(async (token) => await _httpService.GetAsync<UserSearchResponse>(url, token));
         return response.ExtractErrorCode<DummyError, UserSearchResponse>();
      }

      public async Task<Either<VerifyEmailAddressError, VerifyEmailAddressResponse>> VerifyUserEmailAddressAsync(VerifyEmailAddressRequest verificationInfo)
      {
         string url = $"{_baseUserUrl}/verify";
         var (_, response) = await _httpService.PostAsync<VerifyEmailAddressRequest, VerifyEmailAddressResponse>(url, verificationInfo);

         return response.ExtractErrorCode<VerifyEmailAddressError, VerifyEmailAddressResponse>();
      }

      public async Task<Either<UploadTransferError, UploadTransferResponse>> UploadMessageTransferAsync(UploadMessageTransferRequest uploadRequest, Guid recipient, bool withAuthentication)
      {
         string url = recipient == Guid.Empty
            ? $"{_baseTransferUrl}/message"
            : $"{_baseTransferUrl}/message/{recipient}";

         var (_, response) = withAuthentication
            ? await UseAuthenticationMiddleware(async (token) => await _httpService.PostAsync<UploadMessageTransferRequest, UploadTransferResponse>(url, uploadRequest, token))
            : await _httpService.PostAsync<UploadMessageTransferRequest, UploadTransferResponse>(url, uploadRequest);

         return response.ExtractErrorCode<UploadTransferError, UploadTransferResponse>();
      }

      public async Task<Either<UploadTransferError, UploadTransferResponse>> UploadFileTransferAsync(UploadFileTransferRequest uploadRequest, Guid recipient, bool withAuthentication)
      {
         string url = recipient == Guid.Empty
            ? $"{_baseTransferUrl}/file"
            : $"{_baseTransferUrl}/file/{recipient}";

         var (_, response) = withAuthentication
            ? await UseAuthenticationMiddleware(async (token) => await _httpService.PostAsync<UploadFileTransferRequest, UploadTransferResponse>(url, uploadRequest, token))
            : await _httpService.PostAsync<UploadFileTransferRequest, UploadTransferResponse>(url, uploadRequest);

         return response.ExtractErrorCode<UploadTransferError, UploadTransferResponse>();
      }

      public async Task<Either<DownloadTransferPreviewError, DownloadTransferMessagePreviewResponse>> DownloadMessagePreviewAsync(DownloadTransferPreviewRequest downloadRequest, bool withAuthentication)
      {
         string url = $"{_baseTransferUrl}/message/preview";
         var (_, response) = withAuthentication
            ? await UseAuthenticationMiddleware(async (token) => await _httpService.PostAsync<DownloadTransferPreviewRequest, DownloadTransferMessagePreviewResponse>(url, downloadRequest, token))
            : await _httpService.PostAsync<DownloadTransferPreviewRequest, DownloadTransferMessagePreviewResponse>(url, downloadRequest);

         return response.ExtractErrorCode<DownloadTransferPreviewError, DownloadTransferMessagePreviewResponse>();
      }

      public async Task<Either<DownloadTransferSignatureError, DownloadTransferSignatureResponse>> DownloadMessageSignatureAsync(DownloadTransferSignatureRequest downloadRequest, bool withAuthentication)
      {
         string url = $"{_baseTransferUrl}/message/signature";
         var (_, response) = withAuthentication
            ? await UseAuthenticationMiddleware(async (token) => await _httpService.PostAsync<DownloadTransferSignatureRequest, DownloadTransferSignatureResponse>(url, downloadRequest, token))
            : await _httpService.PostAsync<DownloadTransferSignatureRequest, DownloadTransferSignatureResponse>(url, downloadRequest);

         return response.ExtractErrorCode<DownloadTransferSignatureError, DownloadTransferSignatureResponse>();
      }

      public async Task<Either<DownloadTransferCiphertextError, DownloadTransferCiphertextResponse>> DownloadMessageCiphertextAsync(DownloadTransferCiphertextRequest downloadRequest, bool withAuthentication)
      {
         string url = $"{_baseTransferUrl}/message/ciphertext";
         var (_, response) = withAuthentication
            ? await UseAuthenticationMiddleware(async (token) => await _httpService.PostAsync<DownloadTransferCiphertextRequest, DownloadTransferCiphertextResponse>(url, downloadRequest, token))
            : await _httpService.PostAsync<DownloadTransferCiphertextRequest, DownloadTransferCiphertextResponse>(url, downloadRequest);

         return response.ExtractErrorCode<DownloadTransferCiphertextError, DownloadTransferCiphertextResponse>();
      }

      public async Task<Either<DownloadTransferPreviewError, DownloadTransferFilePreviewResponse>> DownloadFilePreviewAsync(DownloadTransferPreviewRequest downloadRequest, bool withAuthentication)
      {
         string url = $"{_baseTransferUrl}/file/preview";
         var (_, response) = withAuthentication
            ? await UseAuthenticationMiddleware(async (token) => await _httpService.PostAsync<DownloadTransferPreviewRequest, DownloadTransferFilePreviewResponse>(url, downloadRequest, token))
            : await _httpService.PostAsync<DownloadTransferPreviewRequest, DownloadTransferFilePreviewResponse>(url, downloadRequest);

         return response.ExtractErrorCode<DownloadTransferPreviewError, DownloadTransferFilePreviewResponse>();
      }

      public async Task<Either<DownloadTransferSignatureError, DownloadTransferSignatureResponse>> DownloadFileSignatureAsync(DownloadTransferSignatureRequest downloadRequest, bool withAuthentication)
      {
         string url = $"{_baseTransferUrl}/file/signature";
         var (_, response) = withAuthentication
            ? await UseAuthenticationMiddleware(async (token) => await _httpService.PostAsync<DownloadTransferSignatureRequest, DownloadTransferSignatureResponse>(url, downloadRequest, token))
            : await _httpService.PostAsync<DownloadTransferSignatureRequest, DownloadTransferSignatureResponse>(url, downloadRequest);

         return response.ExtractErrorCode<DownloadTransferSignatureError, DownloadTransferSignatureResponse>();
      }

      public async Task<Either<DownloadTransferCiphertextError, DownloadTransferCiphertextResponse>> DownloadFileCiphertextAsync(DownloadTransferCiphertextRequest downloadRequest, bool withAuthentication)
      {
         string url = $"{_baseTransferUrl}/file/ciphertext";
         var (_, response) = withAuthentication
            ? await UseAuthenticationMiddleware(async (token) => await _httpService.PostAsync<DownloadTransferCiphertextRequest, DownloadTransferCiphertextResponse>(url, downloadRequest, token))
            : await _httpService.PostAsync<DownloadTransferCiphertextRequest, DownloadTransferCiphertextResponse>(url, downloadRequest);

         return response.ExtractErrorCode<DownloadTransferCiphertextError, DownloadTransferCiphertextResponse>();
      }
   }
}
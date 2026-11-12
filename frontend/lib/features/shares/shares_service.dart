import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/generated/api_client.dart';
import '../../api/generated/models/item_details.dart';
import '../../api/generated/models/item_entry.dart';
import '../../api/generated/models/share_expiry.dart';
import '../../api/generated/models/share_link.dart';
import '../../api/generated/models/user_summary.dart';
import '../../core/api/api_providers.dart';
import 'shared_file_source.dart';

final sharesServiceProvider = Provider<SharesService>(
  (ref) =>
      SharesService(ref.watch(apiClientProvider), ref.watch(dioProvider), Uri.parse(ref.watch(apiBaseUrlProvider))),
);

class SharesService {
  const SharesService(this._api, this._dio, this._baseUri);

  static const _notFound = 404;

  final ApiClient _api;
  final Dio _dio;
  final Uri _baseUri;

  Future<ShareLink?> link(String id) async {
    try {
      return await _api.shares.getItemShare(id: id);
    } on DioException catch (error) {
      if (error.response?.statusCode == _notFound) {
        return null;
      }
      rethrow;
    }
  }

  Future<ShareLink> share(String id) => _api.shares.shareItem(id: id);

  Future<ShareLink> changeExpiry(String id, DateTime? expires) =>
      _api.shares.changeItemShareExpiry(id: id, body: ShareExpiry(expires: expires?.toUtc()));

  Future<void> stopSharing(String id) => _api.shares.stopSharingItem(id: id);

  Future<ItemEntry> sharedItem(String token) => _api.shares.getSharedItem(token: token);

  Future<ItemEntry> join(String token) => _api.shares.joinSharedItem(token: token);

  Future<ItemDetails> sharedDetails(String token, String id) => _api.shares.getSharedItemDetails(token: token, id: id);

  SharedFileSource files(String token) => SharedFileSource(_dio, _baseUri, token);

  Future<List<UserSummary>> recipients(String id) => _api.directShares.getItemRecipients(id: id);

  Future<List<UserSummary>> addRecipient(String id, String userId) =>
      _api.directShares.addItemRecipient(id: id, userId: userId);

  Future<void> removeRecipient(String id, String userId) =>
      _api.directShares.removeItemRecipient(id: id, userId: userId);

  Future<List<UserSummary>> searchUsers(String query) => _api.userSearch.searchUsers(query: query);

  Future<List<ItemEntry>> sharedWithMe() => _api.directShares.getSharedWithMe();
}

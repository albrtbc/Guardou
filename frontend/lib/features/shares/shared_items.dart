import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/generated/models/item_details.dart';
import '../../api/generated/models/item_entry.dart';
import '../../core/providers/retry.dart';
import '../auth/session_controller.dart';
import '../files/files_service.dart';
import 'shares_service.dart';

typedef SharedItemKey = ({String token, String id});

final sharedRootProvider = FutureProvider.autoDispose.family<ItemEntry, String>(
  (ref, token) => ref.read(sharesServiceProvider).sharedItem(token),
  retry: noRetry,
);

final sharedDetailsProvider = FutureProvider.autoDispose.family<ItemDetails, SharedItemKey>(
  (ref, key) => ref.read(sharesServiceProvider).sharedDetails(key.token, key.id),
  retry: noRetry,
);

/// Whether the signed-in user already reaches the item in the app, as its owner or as a recipient.
final sharedItemOpenableProvider = FutureProvider.autoDispose.family<bool, String>((ref, id) async {
  if (await ref.watch(sessionControllerProvider.future) == null) {
    return false;
  }
  try {
    await ref.read(filesServiceProvider).item(id);
    return true;
  } on DioException {
    return false;
  }
}, retry: noRetry);

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/generated/models/share_link.dart';
import '../../core/providers/retry.dart';
import 'share_access.dart';
import 'shares_service.dart';

final shareControllerProvider = AsyncNotifierProvider.autoDispose.family<ShareController, ShareLink?, String>(
  ShareController.new,
  retry: noRetry,
);

class ShareController extends AsyncNotifier<ShareLink?> {
  ShareController(this.itemId);

  final String itemId;

  SharesService get _shares => ref.read(sharesServiceProvider);

  @override
  Future<ShareLink?> build() => _shares.link(itemId);

  Future<void> change(ShareAccess access) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => switch (access) {
        ShareAccess.anyoneWithLink => _shares.share(itemId),
        ShareAccess.restricted => _shares.stopSharing(itemId).then((_) => null),
      },
    );
  }

  Future<void> changeExpiry(DateTime? expires) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => _shares.changeExpiry(itemId, expires));
  }
}

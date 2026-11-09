import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/generated/models/item_details.dart';
import '../../app/router/routes.dart';
import '../../app/theme/app_theme.dart';
import '../../core/widgets/source_code_link.dart';
import '../files/file_view_settings.dart';
import '../viewer/media_siblings.dart';
import '../viewer/media_viewer.dart';
import 'shared_items.dart';
import 'shared_join_button.dart';
import 'shared_link_states.dart';
import 'shares_service.dart';

class SharedViewerPage extends ConsumerWidget {
  const SharedViewerPage({super.key, required this.token, required this.fileId});

  final String token;
  final String fileId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final key = (token: token, id: fileId);
    return Theme(
      data: AppTheme.dark(),
      child: switch (ref.watch(sharedDetailsProvider(key))) {
        AsyncValue(hasValue: true, :final value?) => _viewer(ref, value),
        AsyncValue(:final error?) => SharedLinkError(
          error: error,
          onRetry: () => ref.invalidate(sharedDetailsProvider(key)),
        ),
        _ => const SharedLoadingPage(),
      },
    );
  }

  Widget _viewer(WidgetRef ref, ItemDetails file) {
    final parentId = file.entry.parentId;
    final close = parentId == null ? null : Routes.sharedFolder(token, parentId);
    final siblings = parentId == null
        ? const <Never>[]
        : ref.watch(sharedDetailsProvider((token: token, id: parentId))).value?.items ?? const [];

    return MediaViewer(
      entry: file.entry,
      source: ref.watch(sharesServiceProvider).files(token),
      siblings: mediaEntries(siblings, ref.watch(fileViewSettingsProvider)),
      viewerRoute: (id) => Routes.sharedViewer(token, id),
      leading: close == null ? const SharedBrand() : ViewerCloseButton(location: close),
      closeLocation: close,
      actions: [SharedJoinButton(token: token, itemId: file.entry.id, location: Routes.viewer(file.entry.id))],
      footer: const SourceCodeLink(),
    );
  }
}

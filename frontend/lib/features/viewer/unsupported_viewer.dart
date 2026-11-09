import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/item_entry.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/browser/browser_files.dart';
import '../../l10n/generated/app_localizations.dart';
import '../files/file_source.dart';
import '../files/item_type_presentation.dart';

class UnsupportedViewer extends ConsumerWidget {
  const UnsupportedViewer({super.key, required this.entry, required this.source});

  static const _iconSize = 64.0;

  final ItemEntry entry;
  final FileSource source;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final spacing = AppTokens.of(context).spacing;
    final browser = ref.watch(browserFilesProvider);

    return Center(
      child: SingleChildScrollView(
        padding: EdgeInsets.all(spacing),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ItemTypeIcon(entry.type, size: _iconSize),
            SizedBox(height: spacing),
            Text(l10n.viewerUnsupported, style: Theme.of(context).textTheme.titleMedium, textAlign: TextAlign.center),
            SizedBox(height: spacing),
            FilledButton.icon(
              onPressed: () => browser.download(source.downloadUri(entry.id)),
              icon: const Icon(FluentIcons.arrow_download_24_regular),
              label: Text(l10n.actionDownload),
            ),
          ],
        ),
      ),
    );
  }
}

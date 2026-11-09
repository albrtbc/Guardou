import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/item_entry.dart';
import '../../core/browser/browser_files.dart';
import '../../l10n/generated/app_localizations.dart';
import '../files/file_source.dart';
import 'viewer_content.dart';

class ViewerFrame extends ConsumerWidget {
  const ViewerFrame({
    super.key,
    required this.entry,
    required this.source,
    required this.leading,
    this.overlays = const [],
    this.actions = const [],
    this.footer,
  });

  final ItemEntry entry;
  final FileSource source;
  final Widget leading;
  final List<Widget> overlays;
  final List<Widget> actions;
  final Widget? footer;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final browser = ref.watch(browserFilesProvider);

    return Scaffold(
      bottomNavigationBar: footer,
      appBar: AppBar(
        leading: leading,
        title: Text(entry.name, overflow: TextOverflow.ellipsis),
        actions: [
          ...actions,
          IconButton(
            tooltip: l10n.actionDownload,
            onPressed: () => browser.download(source.downloadUri(entry.id)),
            icon: const Icon(FluentIcons.arrow_download_24_regular),
          ),
        ],
      ),
      body: Stack(
        children: [
          Positioned.fill(
            child: ViewerContent(key: ValueKey(entry.id), entry: entry, source: source),
          ),
          ...overlays,
        ],
      ),
    );
  }
}

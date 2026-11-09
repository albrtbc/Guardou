import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../../api/generated/models/item_kind.dart';
import '../../../app/router/routes.dart';
import '../../../core/layout/window_size_class.dart';
import '../../../l10n/generated/app_localizations.dart';
import '../file_arrangement.dart';
import '../file_presentation.dart';
import '../files_service.dart';
import '../folder_controller.dart';
import 'name_dialog.dart';

Future<String?> showDestinationPicker(
  BuildContext context, {
  required String title,
  required String confirmLabel,
  required String startFolderId,
  required Set<String> excludedIds,
}) {
  final picker = _DestinationPicker(
    title: title,
    confirmLabel: confirmLabel,
    startFolderId: startFolderId,
    excludedIds: excludedIds,
  );
  return showDialog<String>(
    context: context,
    builder: (context) => WindowSizeClass.of(context) == WindowSizeClass.compact
        ? Dialog.fullscreen(child: picker)
        : Dialog(
            child: ConstrainedBox(constraints: const BoxConstraints(maxWidth: 480, maxHeight: 560), child: picker),
          ),
  );
}

class _DestinationPicker extends ConsumerStatefulWidget {
  const _DestinationPicker({
    required this.title,
    required this.confirmLabel,
    required this.startFolderId,
    required this.excludedIds,
  });

  final String title;
  final String confirmLabel;
  final String startFolderId;
  final Set<String> excludedIds;

  @override
  ConsumerState<_DestinationPicker> createState() => _DestinationPickerState();
}

class _DestinationPickerState extends ConsumerState<_DestinationPicker> {
  late String _folderId = widget.startFolderId;

  Future<void> _createFolder() async {
    final l10n = AppLocalizations.of(context);
    final name = await showNameDialog(
      context,
      title: l10n.newFolder,
      confirmLabel: l10n.create,
      initialName: l10n.untitledFolder,
    );
    if (name == null) {
      return;
    }
    final folder = await ref.read(filesServiceProvider).createFolder(_folderId, name);
    ref.invalidate(folderControllerProvider);
    ref.invalidate(childrenControllerProvider);
    setState(() => _folderId = folder.id);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final folder = ref.watch(folderControllerProvider(_folderId)).value;
    final parentId = folder?.entry.parentId;
    final subfolders = [
      ...?folder?.items?.where((item) => item.kind == ItemKind.folder && !widget.excludedIds.contains(item.id)),
    ]..sort((a, b) => compareNatural(a.name, b.name));
    final canAdd = folder?.entry.permissions.canAddChildren ?? false;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(24, 24, 24, 8),
          child: Text(widget.title, style: Theme.of(context).textTheme.headlineSmall),
        ),
        ListTile(
          leading: parentId == null
              ? const Icon(FluentIcons.home_24_regular)
              : IconButton(
                  tooltip: l10n.parentFolder,
                  icon: const Icon(FluentIcons.arrow_left_24_regular),
                  onPressed: () => setState(() => _folderId = parentId),
                ),
          title: Text(folder == null ? l10n.loading : folderLabel(folder.entry, l10n)),
        ),
        const Divider(height: 1),
        Expanded(
          child: folder == null
              ? const Center(child: CircularProgressIndicator())
              : ListView(
                  children: [
                    for (final subfolder in subfolders)
                      ListTile(
                        leading: const Icon(FluentIcons.folder_24_regular),
                        title: Text(subfolder.name, overflow: TextOverflow.ellipsis),
                        trailing: const Icon(FluentIcons.chevron_right_24_regular),
                        onTap: () => setState(() => _folderId = subfolder.id),
                      ),
                  ],
                ),
        ),
        const Divider(height: 1),
        Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              IconButton(
                tooltip: l10n.newFolder,
                onPressed: canAdd ? _createFolder : null,
                icon: const Icon(FluentIcons.folder_add_24_regular),
              ),
              const Spacer(),
              TextButton(onPressed: () => Navigator.of(context).pop(), child: Text(l10n.cancel)),
              const SizedBox(width: 8),
              FilledButton(
                onPressed: canAdd ? () => Navigator.of(context).pop(folder?.entry.id ?? Routes.rootFolderId) : null,
                child: Text(widget.confirmLabel),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

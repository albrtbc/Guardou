import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/item_entry.dart';
import '../../app/theme/app_tokens.dart';
import '../../l10n/generated/app_localizations.dart';
import 'share_access.dart';
import 'share_controller.dart';
import 'share_recipients.dart';

Future<void> showShareDialog(BuildContext context, ItemEntry entry) => showDialog<void>(
  context: context,
  builder: (_) => ShareDialog(entry: entry),
);

class ShareDialog extends ConsumerWidget {
  const ShareDialog({super.key, required this.entry});

  static const _width = 480.0;
  static const _sectionHeight = 56.0;

  final ItemEntry entry;

  Future<void> _copy(BuildContext context, String link) async {
    final messenger = ScaffoldMessenger.of(context);
    final copied = AppLocalizations.of(context).shareLinkCopied;
    await Clipboard.setData(ClipboardData(text: link));
    messenger
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(copied)));
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final spacing = AppTokens.of(context).spacing;
    final provider = shareControllerProvider(entry.id);
    final state = ref.watch(provider);
    final link = state is AsyncData<String?> ? state.value : null;

    return AlertDialog(
      title: Text(l10n.shareTitle(entry.name), overflow: TextOverflow.ellipsis),
      content: SizedBox(
        width: _width,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(l10n.shareWithPeople, style: Theme.of(context).textTheme.titleSmall),
              SizedBox(height: spacing),
              ShareRecipients(
                itemId: entry.id,
                onError: (onRetry) => _ErrorRow(onRetry: onRetry),
              ),
              SizedBox(height: spacing),
              Text(l10n.shareGeneralAccess, style: Theme.of(context).textTheme.titleSmall),
              SizedBox(height: spacing / 2),
              SizedBox(
                height: _sectionHeight,
                child: switch (state) {
                  AsyncData(:final value) => _AccessSelector(
                    access: ShareAccess.of(value),
                    onChanged: (access) => ref.read(provider.notifier).change(access),
                  ),
                  AsyncError() => _ErrorRow(onRetry: () => ref.invalidate(provider)),
                  _ => Center(child: CircularProgressIndicator(semanticsLabel: l10n.loading)),
                },
              ),
              if (link != null) ...[
                SizedBox(height: spacing),
                InputDecorator(
                  decoration: InputDecoration(labelText: l10n.shareLink),
                  child: SelectableText(link, maxLines: 1),
                ),
              ],
            ],
          ),
        ),
      ),
      actions: [
        if (link != null)
          OutlinedButton.icon(
            onPressed: () => _copy(context, link),
            icon: const Icon(FluentIcons.link_24_regular),
            label: Text(l10n.shareCopyLink),
          ),
        FilledButton(onPressed: () => Navigator.of(context).pop(), child: Text(l10n.done)),
      ],
    );
  }
}

class _AccessSelector extends StatelessWidget {
  const _AccessSelector({required this.access, required this.onChanged});

  final ShareAccess access;
  final ValueChanged<ShareAccess> onChanged;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final spacing = AppTokens.of(context).spacing;

    return MenuAnchor(
      builder: (context, controller, _) => Material(
        color: theme.colorScheme.surfaceContainerHighest,
        shape: const StadiumBorder(),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: () => controller.isOpen ? controller.close() : controller.open(),
          child: Padding(
            padding: EdgeInsets.symmetric(horizontal: spacing),
            child: Row(
              children: [
                Icon(access.icon),
                SizedBox(width: spacing),
                Expanded(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(access.label(l10n), style: theme.textTheme.titleSmall, overflow: TextOverflow.ellipsis),
                      Text(access.description(l10n), style: theme.textTheme.bodySmall, overflow: TextOverflow.ellipsis),
                    ],
                  ),
                ),
                SizedBox(width: spacing),
                const Icon(FluentIcons.chevron_down_24_regular),
              ],
            ),
          ),
        ),
      ),
      menuChildren: [
        for (final option in ShareAccess.values)
          MenuItemButton(
            leadingIcon: Icon(option.icon),
            trailingIcon: option == access ? const Icon(FluentIcons.checkmark_24_regular) : null,
            onPressed: option == access ? null : () => onChanged(option),
            child: Text(option.label(l10n)),
          ),
      ],
    );
  }
}

class _ErrorRow extends StatelessWidget {
  const _ErrorRow({required this.onRetry});

  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return Row(
      children: [
        Expanded(
          child: Text(l10n.genericError, style: TextStyle(color: Theme.of(context).colorScheme.error)),
        ),
        TextButton(onPressed: onRetry, child: Text(l10n.errorRetry)),
      ],
    );
  }
}

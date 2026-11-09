import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/item_entry.dart';
import '../../app/theme/app_tokens.dart';
import '../../l10n/generated/app_localizations.dart';
import '../files/file_presentation.dart';
import 'search_controller.dart';
import 'search_filter_chips.dart';
import '../files/item_type_presentation.dart';
import '../files/item_marks.dart';

class SearchPanel extends ConsumerWidget {
  const SearchPanel({super.key, required this.onOpen});

  final ValueChanged<ItemEntry> onOpen;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final spacing = AppTokens.of(context).spacing;
    final search = ref.watch(itemSearchControllerProvider);

    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: EdgeInsets.fromLTRB(spacing, spacing / 2, spacing, spacing / 2),
          child: const SearchFilterChips(),
        ),
        const Divider(height: 1),
        Flexible(
          child: _SearchResults(results: search.results, onOpen: onOpen),
        ),
      ],
    );
  }
}

class _SearchResults extends StatelessWidget {
  const _SearchResults({required this.results, required this.onOpen});

  final AsyncValue<List<ItemEntry>>? results;
  final ValueChanged<ItemEntry> onOpen;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return switch (results) {
      null => _SearchMessage(text: l10n.searchPrompt(ItemSearchController.minimumLength)),
      AsyncData(:final value) when value.isEmpty => _SearchMessage(text: l10n.searchNoResults),
      AsyncData(:final value) => ListView(
        shrinkWrap: true,
        children: [for (final entry in value) _SearchResultTile(entry: entry, onOpen: onOpen)],
      ),
      AsyncError() => _SearchMessage(text: l10n.searchFailed),
      _ => Padding(
        padding: EdgeInsets.all(AppTokens.of(context).spacing),
        child: Center(child: CircularProgressIndicator(semanticsLabel: l10n.loading)),
      ),
    };
  }
}

class _SearchResultTile extends StatelessWidget {
  const _SearchResultTile({required this.entry, required this.onOpen});

  static const _iconSize = 24.0;

  final ItemEntry entry;
  final ValueChanged<ItemEntry> onOpen;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return ListTile(
      leading: ItemTypeIcon(entry.type, size: _iconSize),
      title: ItemName(entry: entry),
      subtitle: Text(
        l10n.fileSummary(formatDate(entry.modified, l10n.localeName), formatSize(entry.size, l10n.localeName)),
        overflow: TextOverflow.ellipsis,
      ),
      trailing: entry.permissions.canModify ? null : Icon(FluentIcons.people_24_regular, semanticLabel: l10n.readOnly),
      onTap: () => onOpen(entry),
    );
  }
}

class _SearchMessage extends StatelessWidget {
  const _SearchMessage({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: EdgeInsets.all(AppTokens.of(context).spacing),
      child: Text(
        text,
        textAlign: TextAlign.center,
        style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
      ),
    );
  }
}

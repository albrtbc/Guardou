import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/user_summary.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/widgets/user_avatar.dart';
import '../../l10n/generated/app_localizations.dart';
import 'recipients_controller.dart';
import 'shares_service.dart';

class ShareRecipients extends ConsumerWidget {
  const ShareRecipients({super.key, required this.itemId, required this.onError});

  static const _loadingHeight = 56.0;

  final String itemId;
  final Widget Function(VoidCallback onRetry) onError;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final provider = recipientsControllerProvider(itemId);
    final controller = ref.read(provider.notifier);

    return switch (ref.watch(provider)) {
      AsyncData(:final value) => Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _RecipientSearch(excluded: {for (final user in value) user.id}, onSelected: controller.add),
          SizedBox(height: AppTokens.of(context).spacing / 2),
          if (value.isEmpty)
            ListTile(contentPadding: EdgeInsets.zero, title: Text(l10n.shareNoPeople))
          else
            for (final user in value) _RecipientTile(user: user, onRemove: () => controller.remove(user)),
        ],
      ),
      AsyncError() => onError(() => ref.invalidate(provider)),
      _ => SizedBox(
        height: _loadingHeight,
        child: Center(child: CircularProgressIndicator(semanticsLabel: l10n.loading)),
      ),
    };
  }
}

class _RecipientTile extends StatelessWidget {
  const _RecipientTile({required this.user, required this.onRemove});

  static const _avatarRadius = 18.0;

  final UserSummary user;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) => ListTile(
    contentPadding: EdgeInsets.zero,
    leading: UserAvatar(userId: user.id, name: user.displayName, hasPicture: user.hasPicture, radius: _avatarRadius),
    title: Text(user.displayName, overflow: TextOverflow.ellipsis),
    subtitle: Text(user.email, overflow: TextOverflow.ellipsis),
    trailing: IconButton(
      tooltip: AppLocalizations.of(context).shareRemoveAccess,
      onPressed: onRemove,
      icon: const Icon(FluentIcons.dismiss_24_regular),
    ),
  );
}

class _RecipientSearch extends ConsumerStatefulWidget {
  const _RecipientSearch({required this.excluded, required this.onSelected});

  final Set<String> excluded;
  final ValueChanged<UserSummary> onSelected;

  @override
  ConsumerState<_RecipientSearch> createState() => _RecipientSearchState();
}

class _RecipientSearchState extends ConsumerState<_RecipientSearch> {
  static const _optionsMaxHeight = 240.0;
  static const _avatarRadius = 16.0;

  final _text = TextEditingController();
  final _focus = FocusNode();

  @override
  void dispose() {
    _text.dispose();
    _focus.dispose();
    super.dispose();
  }

  Future<Iterable<UserSummary>> _options(TextEditingValue value) async {
    final query = value.text.trim();
    if (query.isEmpty) {
      return const [];
    }
    final users = await ref.read(sharesServiceProvider).searchUsers(query);
    return users.where((user) => !widget.excluded.contains(user.id));
  }

  void _select(UserSummary user) {
    _text.clear();
    widget.onSelected(user);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return LayoutBuilder(
      builder: (context, constraints) => RawAutocomplete<UserSummary>(
        textEditingController: _text,
        focusNode: _focus,
        displayStringForOption: (user) => user.displayName,
        optionsBuilder: _options,
        onSelected: _select,
        fieldViewBuilder: (context, controller, focusNode, onSubmitted) => TextField(
          controller: controller,
          focusNode: focusNode,
          onSubmitted: (_) => onSubmitted(),
          decoration: InputDecoration(
            labelText: l10n.shareAddPeople,
            prefixIcon: const Icon(FluentIcons.person_add_24_regular),
          ),
        ),
        optionsViewBuilder: (context, onSelected, options) => Align(
          alignment: AlignmentDirectional.topStart,
          child: Material(
            elevation: 4,
            borderRadius: BorderRadius.circular(8),
            child: ConstrainedBox(
              constraints: BoxConstraints(maxHeight: _optionsMaxHeight, maxWidth: constraints.maxWidth),
              child: ListView(
                padding: EdgeInsets.zero,
                shrinkWrap: true,
                children: [
                  for (final user in options)
                    ListTile(
                      leading: UserAvatar(
                        userId: user.id,
                        name: user.displayName,
                        hasPicture: user.hasPicture,
                        radius: _avatarRadius,
                      ),
                      title: Text(user.displayName, overflow: TextOverflow.ellipsis),
                      subtitle: Text(user.email, overflow: TextOverflow.ellipsis),
                      onTap: () => onSelected(user),
                    ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

import 'package:clock/clock.dart';
import 'package:flutter/material.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../app/theme/app_tokens.dart';
import '../../l10n/generated/app_localizations.dart';
import '../files/file_presentation.dart';

class ShareExpiryRow extends StatelessWidget {
  const ShareExpiryRow({super.key, required this.expires, required this.onChanged});

  static const _defaultDays = 7;
  static const _maximumYears = 10;

  final DateTime? expires;
  final ValueChanged<DateTime?> onChanged;

  Future<void> _pick(BuildContext context) async {
    final now = clock.now();
    final today = DateTime(now.year, now.month, now.day);
    final current = expires?.toLocal();
    final initial = current == null || current.isBefore(today) ? today.add(const Duration(days: _defaultDays)) : current;
    final date = await showDatePicker(
      context: context,
      initialDate: initial,
      firstDate: today,
      lastDate: DateTime(today.year + _maximumYears, today.month, today.day),
    );
    if (date != null) {
      onChanged(DateTime(date.year, date.month, date.day, 23, 59, 59));
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final value = expires;
    final expired = value != null && !value.isAfter(clock.now());
    final color = expired ? theme.colorScheme.error : null;
    final locale = Localizations.localeOf(context).toLanguageTag();
    final text = switch (value) {
      null => l10n.shareNoExpiry,
      _ when expired => l10n.shareExpired(formatDate(value, locale)),
      _ => l10n.shareExpires(formatDate(value, locale)),
    };

    return Padding(
      padding: EdgeInsets.only(top: AppTokens.of(context).spacing / 2),
      child: Row(
        children: [
          Expanded(child: Text(text, style: theme.textTheme.bodyMedium?.copyWith(color: color))),
          IconButton(
            tooltip: l10n.shareSetExpiry,
            onPressed: () => _pick(context),
            icon: const Icon(FluentIcons.calendar_edit_24_regular),
          ),
          if (value != null)
            IconButton(
              tooltip: l10n.shareRemoveExpiry,
              onPressed: () => onChanged(null),
              icon: const Icon(FluentIcons.dismiss_24_regular),
            ),
        ],
      ),
    );
  }
}

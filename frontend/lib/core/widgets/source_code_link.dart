import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/theme/app_tokens.dart';
import '../../l10n/generated/app_localizations.dart';
import '../browser/browser_location.dart';

/// Where visitors find the source code of the running version (AGPL-3.0 section 13).
const sourceCodeUrl = String.fromEnvironment('SOURCE_CODE_URL', defaultValue: 'https://github.com/albrtbc/Guardou');

class SourceCodeLink extends ConsumerWidget {
  const SourceCodeLink({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final spacing = AppTokens.of(context).spacing;

    return Padding(
      padding: EdgeInsets.symmetric(vertical: spacing / 2),
      child: Center(
        heightFactor: 1,
        child: TextButton(
          onPressed: () => ref.read(browserLocationProvider).open(Uri.parse(sourceCodeUrl)),
          style: TextButton.styleFrom(
            foregroundColor: theme.colorScheme.onSurfaceVariant,
            visualDensity: VisualDensity.compact,
          ),
          child: Text(AppLocalizations.of(context).sourceCodeLink, style: theme.textTheme.bodySmall),
        ),
      ),
    );
  }
}

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../app/router/routes.dart';
import '../../app/theme/app_tokens.dart';
import '../../l10n/generated/app_localizations.dart';
import '../files/dialogs/confirm_dialog.dart';
import '../files/dialogs/conflict_dialog.dart';
import '../files/file_presentation.dart';
import '../files/item_type_presentation.dart';
import 'upload_item.dart';
import 'upload_queue_controller.dart';

class UploadPanel extends ConsumerStatefulWidget {
  const UploadPanel({super.key, required this.compact});

  static const width = 360.0;

  final bool compact;

  @override
  ConsumerState<UploadPanel> createState() => _UploadPanelState();
}

class _UploadPanelState extends ConsumerState<UploadPanel> {
  static const _maximumListHeight = 280.0;

  bool _expanded = true;

  UploadQueueController get _queue => ref.read(uploadQueueProvider.notifier);

  @override
  void initState() {
    super.initState();
    ref.listenManual(uploadQueueProvider.select((state) => state.conflict), (_, conflict) {
      if (conflict != null) {
        _resolve(conflict);
      }
    }, fireImmediately: true);
  }

  Future<void> _resolve(UploadConflictRequest conflict) async {
    await Future<void>.delayed(Duration.zero);
    if (mounted) {
      _queue.resolveConflict(await showConflictDialog(context, conflict.name));
    }
  }

  Future<void> _close(UploadQueueState state) async {
    if (!state.isActive) {
      _queue.clear();
      return;
    }
    final l10n = AppLocalizations.of(context);
    final confirmed = await showConfirmDialog(
      context,
      title: l10n.cancelUploadsTitle,
      message: l10n.cancelUploadsMessage,
      confirmLabel: l10n.cancelUploads,
    );
    if (confirmed) {
      _queue
        ..cancelAll()
        ..clear();
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(uploadQueueProvider);
    if (state.items.isEmpty) {
      return const SizedBox.shrink();
    }
    final tokens = AppTokens.of(context);
    final colors = Theme.of(context).colorScheme;
    final radius = Radius.circular(tokens.panelRadius);

    return Material(
      color: colors.surfaceContainerHigh,
      elevation: widget.compact ? 0 : 6,
      shape: RoundedRectangleBorder(
        borderRadius: widget.compact ? BorderRadius.zero : BorderRadius.only(topLeft: radius, topRight: radius),
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _Header(
            state: state,
            expanded: _expanded,
            onToggle: () => setState(() => _expanded = !_expanded),
            onClose: () => _close(state),
          ),
          if (_expanded) ...[
            if (state.isActive) _Summary(state: state, onCancelAll: _queue.cancelAll),
            ConstrainedBox(
              constraints: const BoxConstraints(maxHeight: _maximumListHeight),
              child: ListView.builder(
                shrinkWrap: true,
                padding: EdgeInsets.only(bottom: tokens.spacing / 2),
                itemCount: state.items.length,
                itemBuilder: (context, index) =>
                    _UploadRow(item: state.items[index], alwaysShowActions: widget.compact),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.state, required this.expanded, required this.onToggle, required this.onClose});

  final UploadQueueState state;
  final bool expanded;
  final VoidCallback onToggle;
  final VoidCallback onClose;

  String _title(AppLocalizations l10n) {
    if (state.isActive) {
      return l10n.uploadingItems(state.pendingCount);
    }
    return state.failedCount > 0 ? l10n.uploadsFailed(state.failedCount) : l10n.uploadsComplete(state.completedCount);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final spacing = AppTokens.of(context).spacing;

    return Padding(
      padding: EdgeInsets.fromLTRB(spacing * 1.5, spacing / 2, spacing / 2, spacing / 2),
      child: Row(
        children: [
          Expanded(
            child: Text(_title(l10n), style: Theme.of(context).textTheme.titleMedium, overflow: TextOverflow.ellipsis),
          ),
          IconButton(
            tooltip: expanded ? l10n.uploadPanelMinimize : l10n.uploadPanelExpand,
            icon: Icon(expanded ? FluentIcons.chevron_down_24_regular : FluentIcons.chevron_up_24_regular),
            onPressed: onToggle,
          ),
          IconButton(tooltip: l10n.close, icon: const Icon(FluentIcons.dismiss_24_regular), onPressed: onClose),
        ],
      ),
    );
  }
}

class _Summary extends StatelessWidget {
  const _Summary({required this.state, required this.onCancelAll});

  static const _secondsPerMinute = 60;
  static const _minutesPerHour = 60;

  final UploadQueueState state;
  final VoidCallback onCancelAll;

  String _timeLeft(AppLocalizations l10n) {
    final timeLeft = state.timeLeft;
    if (timeLeft == null) {
      return l10n.uploadStarting;
    }
    if (timeLeft.inSeconds < _secondsPerMinute) {
      return l10n.uploadTimeLeftLessThanMinute;
    }
    final minutes = (timeLeft.inSeconds / _secondsPerMinute).ceil();
    return minutes < _minutesPerHour
        ? l10n.uploadTimeLeftMinutes(minutes)
        : l10n.uploadTimeLeftHours((minutes / _minutesPerHour).ceil());
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final spacing = AppTokens.of(context).spacing;

    return ColoredBox(
      color: theme.colorScheme.surfaceContainerHighest,
      child: Padding(
        padding: EdgeInsets.only(left: spacing * 1.5, right: spacing / 2),
        child: Row(
          children: [
            Expanded(child: Text(_timeLeft(l10n), style: theme.textTheme.bodySmall)),
            TextButton(onPressed: onCancelAll, child: Text(l10n.cancel)),
          ],
        ),
      ),
    );
  }
}

class _UploadRow extends ConsumerStatefulWidget {
  const _UploadRow({required this.item, required this.alwaysShowActions});

  final UploadItem item;
  final bool alwaysShowActions;

  @override
  ConsumerState<_UploadRow> createState() => _UploadRowState();
}

class _UploadRowState extends ConsumerState<_UploadRow> {
  static const _indicatorSize = 22.0;
  static const _checkSize = 14.0;
  static const _rowActionHeight = 40.0;

  bool _hovered = false;

  UploadQueueController get _queue => ref.read(uploadQueueProvider.notifier);

  String? _subtitle(AppLocalizations l10n, String locale) {
    final item = widget.item;
    return switch (item.status) {
      UploadStatus.queued => l10n.uploadWaiting,
      UploadStatus.uploading => l10n.uploadProgress(formatSize(item.sent, locale), formatSize(item.size, locale)),
      UploadStatus.paused => l10n.uploadPaused,
      UploadStatus.skipped => l10n.uploadSkipped,
      UploadStatus.cancelled => l10n.uploadCancelled,
      UploadStatus.failed when item.failure == UploadFailure.uploadInProgress => l10n.uploadInProgressElsewhere,
      UploadStatus.failed when item.failure == UploadFailure.quotaExceeded => l10n.uploadQuotaExceeded,
      UploadStatus.failed when item.failure == UploadFailure.fileTooLarge => l10n.uploadFileTooLarge,
      UploadStatus.failed when item.failure == UploadFailure.storageFull => l10n.uploadStorageFull,
      UploadStatus.failed when item.failure == UploadFailure.tooManyUploads => l10n.uploadTooManyUploads,
      UploadStatus.failed => l10n.uploadFailed,
      UploadStatus.completed => null,
    };
  }

  List<Widget> _actions(AppLocalizations l10n) {
    final item = widget.item;
    final cancel = IconButton(
      tooltip: l10n.cancel,
      icon: const Icon(FluentIcons.dismiss_24_regular),
      onPressed: () => _queue.cancel(item.id),
    );
    return switch (item.status) {
      UploadStatus.queued => [cancel],
      UploadStatus.uploading => [
        IconButton(
          tooltip: l10n.uploadPause,
          icon: const Icon(FluentIcons.pause_24_regular),
          onPressed: () => _queue.pause(item.id),
        ),
        cancel,
      ],
      UploadStatus.paused => [
        IconButton(
          tooltip: l10n.uploadResume,
          icon: const Icon(FluentIcons.play_24_regular),
          onPressed: () => _queue.resume(item.id),
        ),
        cancel,
      ],
      UploadStatus.failed => [
        IconButton(
          tooltip: l10n.uploadRetry,
          icon: const Icon(FluentIcons.arrow_clockwise_24_regular),
          onPressed: () => _queue.resume(item.id),
        ),
        cancel,
      ],
      UploadStatus.completed || UploadStatus.skipped || UploadStatus.cancelled => const [],
    };
  }

  Widget _indicator(BuildContext context) {
    final item = widget.item;
    final colors = Theme.of(context).colorScheme;
    final tokens = AppTokens.of(context);
    return switch (item.status) {
      UploadStatus.completed => Container(
        width: _indicatorSize,
        height: _indicatorSize,
        decoration: BoxDecoration(color: tokens.success, shape: BoxShape.circle),
        child: Icon(FluentIcons.checkmark_24_regular, size: _checkSize, color: tokens.onSuccess),
      ),
      UploadStatus.failed => Icon(FluentIcons.error_circle_24_regular, size: _indicatorSize, color: colors.error),
      UploadStatus.skipped || UploadStatus.cancelled => Icon(
        FluentIcons.subtract_circle_24_regular,
        size: _indicatorSize,
        color: colors.onSurfaceVariant,
      ),
      UploadStatus.queued || UploadStatus.uploading || UploadStatus.paused => SizedBox.square(
        dimension: _indicatorSize,
        child: CircularProgressIndicator(
          value: item.progress,
          strokeWidth: 3,
          backgroundColor: colors.surfaceContainerHighest,
        ),
      ),
    };
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final spacing = AppTokens.of(context).spacing;
    final item = widget.item;
    final entry = item.entry;
    final subtitle = _subtitle(l10n, Localizations.localeOf(context).toString());
    final actions = _actions(l10n);
    final showActions = actions.isNotEmpty && (_hovered || widget.alwaysShowActions);

    return MouseRegion(
      onEnter: (_) => setState(() => _hovered = true),
      onExit: (_) => setState(() => _hovered = false),
      child: InkWell(
        onTap: entry == null ? null : () => context.go(Routes.fileItem(entry.id)),
        child: Padding(
          padding: EdgeInsets.symmetric(horizontal: spacing * 1.5, vertical: spacing / 2),
          child: Row(
            children: [
              entry == null
                  ? Icon(FluentIcons.document_24_regular, color: theme.colorScheme.onSurfaceVariant)
                  : ItemTypeIcon(entry.type),
              SizedBox(width: spacing),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(item.name, overflow: TextOverflow.ellipsis),
                    if (subtitle != null)
                      Text(
                        subtitle,
                        overflow: TextOverflow.ellipsis,
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: item.status == UploadStatus.failed
                              ? theme.colorScheme.error
                              : theme.colorScheme.onSurfaceVariant,
                        ),
                      ),
                  ],
                ),
              ),
              SizedBox(width: spacing / 2),
              SizedBox(
                height: _rowActionHeight,
                child: Row(mainAxisSize: MainAxisSize.min, children: showActions ? actions : [_indicator(context)]),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

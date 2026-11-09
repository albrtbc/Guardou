import 'package:flutter/widgets.dart';
import 'package:intl/intl.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/task_entry.dart';
import '../../api/generated/models/task_kind.dart';
import '../../api/generated/models/task_priority.dart';
import '../../api/generated/models/task_state.dart';
import '../../l10n/generated/app_localizations.dart';

String taskStateLabel(TaskState state, AppLocalizations l10n) => switch (state) {
  TaskState.pending => l10n.taskStatePending,
  TaskState.running => l10n.taskStateRunning,
  TaskState.failed => l10n.taskStateFailed,
  TaskState.$unknown => l10n.valueMissing,
};

String queueTitle(TaskKind kind, AppLocalizations l10n) => switch (kind) {
  TaskKind.preview => l10n.queuePreviewTitle,
  TaskKind.searchIndex => l10n.queueIndexTitle,
  TaskKind.$unknown => l10n.valueMissing,
};

String queueDescription(TaskKind kind, AppLocalizations l10n) => switch (kind) {
  TaskKind.preview => l10n.queuePreviewDescription,
  TaskKind.searchIndex => l10n.queueIndexDescription,
  TaskKind.$unknown => '',
};

IconData queueIcon(TaskKind kind) => switch (kind) {
  TaskKind.preview => FluentIcons.image_multiple_24_regular,
  TaskKind.searchIndex => FluentIcons.document_search_24_regular,
  TaskKind.$unknown => FluentIcons.task_list_ltr_24_regular,
};

String taskPriorityLabel(TaskPriority priority, AppLocalizations l10n) => switch (priority) {
  TaskPriority.low => l10n.taskPriorityLow,
  TaskPriority.normal => l10n.taskPriorityNormal,
  TaskPriority.high => l10n.taskPriorityHigh,
  TaskPriority.$unknown => l10n.valueMissing,
};

String taskUpdatedLabel(TaskEntry task, AppLocalizations l10n) =>
    DateFormat.yMMMd(l10n.localeName).add_jm().format(task.updated.toLocal());

bool canRun(TaskEntry task) => task.state != TaskState.running;

bool canReset(TaskEntry task) => task.state == TaskState.failed;

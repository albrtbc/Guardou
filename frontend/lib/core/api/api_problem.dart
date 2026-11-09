import 'package:dio/dio.dart';

class ApiProblem implements Exception {
  const ApiProblem({required this.status, this.title, this.detail, this.codes = const []});

  factory ApiProblem.fromResponse(Response<Object?>? response) {
    final data = response?.data;
    final body = data is Map<String, Object?> ? data : const <String, Object?>{};
    return ApiProblem(
      status: response?.statusCode ?? 0,
      title: body['title'] as String?,
      detail: body['detail'] as String?,
      codes: [...?(body['codes'] as List<Object?>?)?.whereType<String>()],
    );
  }

  final int status;
  final String? title;
  final String? detail;
  final List<String> codes;

  static const quotaExceededCode = 'quotaExceeded';
  static const fileTooLargeCode = 'fileTooLarge';
  static const storageFullCode = 'storageFull';
  static const tooManyUploadsCode = 'tooManyUploads';

  bool get isUnauthorized => status == 401;

  bool get isQuotaExceeded => codes.contains(quotaExceededCode);

  bool get isFileTooLarge => codes.contains(fileTooLargeCode);

  // The tus endpoint answers 507 without a problem body, so the status counts too.
  bool get isStorageFull => codes.contains(storageFullCode) || status == 507;

  // Only the upload cap carries the code; the 429 of the rate limiter has none.
  bool get isTooManyUploads => codes.contains(tooManyUploadsCode);

  @override
  String toString() => 'ApiProblem($status, $title)';
}

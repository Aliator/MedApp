{{/* The release's base name: the release name, with "-medapp" added unless it already says medapp. */}}
{{- define "medapp.fullname" -}}
{{- if contains .Chart.Name .Release.Name -}}
{{- .Release.Name | trunc 50 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name .Chart.Name | trunc 50 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}

{{- define "medapp.labels" -}}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version }}
app.kubernetes.io/name: {{ .Chart.Name }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}

{{/* Selector labels for one component (api, sql, migrations): pass (list . "api"). */}}
{{- define "medapp.selectorLabels" -}}
{{- $root := index . 0 -}}
app.kubernetes.io/name: {{ $root.Chart.Name }}
app.kubernetes.io/instance: {{ $root.Release.Name }}
app.kubernetes.io/component: {{ index . 1 }}
{{- end -}}

{{- define "medapp.image" -}}
{{- if not .Values.image.tag -}}
{{- fail "image.tag is required: the tag of the image built from MedApp.API/Dockerfile." -}}
{{- end -}}
{{- printf "%s:%s" .Values.image.repository .Values.image.tag -}}
{{- end -}}

{{/* The Secret with the connection string (and the sa password): the given one, or the chart's own. */}}
{{- define "medapp.dbSecretName" -}}
{{- .Values.database.existingSecret | default (printf "%s-db" (include "medapp.fullname" .)) -}}
{{- end -}}

{{/* The Secret with the first admin's username and password: the given one, or the chart's own. */}}
{{- define "medapp.firstAdminSecretName" -}}
{{- .Values.firstAdmin.existingSecret | default (printf "%s-first-admin" (include "medapp.fullname" .)) -}}
{{- end -}}

{{/* The Secret with the backplane's connection string (and Valkey's password): the given one, or the chart's own. */}}
{{- define "medapp.backplaneSecretName" -}}
{{- .Values.backplane.existingSecret | default (printf "%s-backplane" (include "medapp.fullname" .)) -}}
{{- end -}}

{{- define "medapp.valkeyName" -}}
{{- printf "%s-valkey" (include "medapp.fullname" .) -}}
{{- end -}}

{{/* The Secret with the attachments' connection string (and Azurite's account key): the given one, or the chart's own. */}}
{{- define "medapp.attachmentsSecretName" -}}
{{- .Values.attachments.existingSecret | default (printf "%s-attachments" (include "medapp.fullname" .)) -}}
{{- end -}}

{{/*
"true" when the API has file storage for attachments, from (in this order) attachments.existingSecret, the secret
store (externalSecrets, unless its attachments connection-string is null), or the chart's own Secret for Azurite.
Empty otherwise: the API runs without file storage, and uploads and downloads answer 503.
*/}}
{{- define "medapp.attachmentsStorage" -}}
{{- if .Values.attachments.existingSecret -}}
true
{{- else if .Values.externalSecrets.enabled -}}
{{- with .Values.externalSecrets.attachments -}}
{{- if index . "connection-string" -}}
true
{{- end -}}
{{- end -}}
{{- else if .Values.attachments.azurite.enabled -}}
true
{{- end -}}
{{- end -}}

{{- define "medapp.azuriteName" -}}
{{- printf "%s-azurite" (include "medapp.fullname" .) -}}
{{- end -}}

{{- define "medapp.sqlName" -}}
{{- printf "%s-sql" (include "medapp.fullname" .) -}}
{{- end -}}

{{/*
Settings every MedApp pod shares, which meet the "restricted" Pod Security Standard: a non-root user (pass it:
(list . 1654)), the runtime's default seccomp profile, and no Kubernetes API token (nothing here calls the API).
*/}}
{{- define "medapp.podSecurity" -}}
automountServiceAccountToken: false
securityContext:
  runAsNonRoot: true
  runAsUser: {{ index . 1 }}
  seccompProfile:
    type: RuntimeDefault
{{- end -}}

{{/* Settings the API's and migrations' containers share: no privilege escalation or capabilities, a read-only root. */}}
{{- define "medapp.containerSecurityContext" -}}
allowPrivilegeEscalation: false
readOnlyRootFilesystem: true
capabilities:
  drop: ["ALL"]
{{- end -}}

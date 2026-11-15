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

{{- define "medapp.sqlName" -}}
{{- printf "%s-sql" (include "medapp.fullname" .) -}}
{{- end -}}

{{/* Settings every MedApp container shares: no root, no privilege escalation, no Linux capabilities. */}}
{{- define "medapp.containerSecurityContext" -}}
allowPrivilegeEscalation: false
capabilities:
  drop: ["ALL"]
{{- end -}}

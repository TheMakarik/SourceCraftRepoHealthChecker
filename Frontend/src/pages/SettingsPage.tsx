import { useEffect, useMemo, useState, type ReactNode } from "react";
import {
  Accordion,
  AccordionHeader,
  AccordionItem,
  AccordionPanel,
  Button,
  Card,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Dropdown,
  Field,
  Input,
  Link,
  Option
} from "@fluentui/react-components";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useAiModels, useMeAi, useMeTokens, useStoreAi, useStoreSourceCraftToken, useTestAi } from "../shared/api/hooks";
import { Key24Regular, Play24Regular, Save24Regular, Sparkle24Regular } from "@fluentui/react-icons";
import type { AiProvider } from "../shared/api/types";
import { providerLabels } from "../shared/api/labels";

const tokenSchema = z.object({ token: z.string().min(1, "Введите токен") });
const aiSchema = z.object({
  model: z.string().min(1, "Выберите модель"),
  baseUrl: z.string().optional()
});

const providers = Object.keys(providerLabels) as AiProvider[];

interface SecretDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: string;
  fieldLabel: string;
  placeholder: string;
  pending: boolean;
  isError: boolean;
  errorMessage: string;
  onSubmit: (token: string) => void;
  children?: ReactNode;
}

function SecretDialog({
  open,
  onOpenChange,
  title,
  description,
  fieldLabel,
  placeholder,
  pending,
  isError,
  errorMessage,
  onSubmit,
  children
}: SecretDialogProps) {
  const form = useForm<z.infer<typeof tokenSchema>>({ resolver: zodResolver(tokenSchema), defaultValues: { token: "" } });

  useEffect(() => {
    if (!open)
      form.reset();
  }, [open, form]);

  const submit = form.handleSubmit((values) => onSubmit(values.token));

  return (
    <Dialog open={open} onOpenChange={(_event, data) => onOpenChange(data.open)} modalType="modal">
      <DialogSurface>
        <DialogBody>
          <DialogTitle>{title}</DialogTitle>
          <DialogContent className="stack">
            <p className="muted" style={{ margin: 0 }}>
              {description}
            </p>
            <Field label={fieldLabel} validationMessage={form.formState.errors.token?.message}>
              <Input type="password" placeholder={placeholder} {...form.register("token")} />
            </Field>
            {children}
            {isError ? <span className="tone-bad">{errorMessage}</span> : null}
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={() => onOpenChange(false)}>
              Отмена
            </Button>
            <Button appearance="primary" onClick={submit} disabled={pending}>
              {pending ? "Сохраняем…" : "Сохранить"}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}

export function SettingsPage() {
  const tokens = useMeTokens(true);
  const meAi = useMeAi(true);
  const modelsQuery = useAiModels();
  const storeToken = useStoreSourceCraftToken();
  const storeAi = useStoreAi();
  const testAi = useTestAi();
  const [provider, setProvider] = useState<AiProvider>("DeepSeek");
  const [patDialogOpen, setPatDialogOpen] = useState(false);
  const [aiTokenDialogOpen, setAiTokenDialogOpen] = useState(false);

  const aiForm = useForm<z.infer<typeof aiSchema>>({
    resolver: zodResolver(aiSchema),
    defaultValues: { model: "", baseUrl: "" }
  });

  const modelsByProvider = useMemo(() => {
    const map = {} as Record<AiProvider, string[]>;
    for (const entry of modelsQuery.data ?? [])
      map[entry.provider] = entry.models;
    return map;
  }, [modelsQuery.data]);

  useEffect(() => {
    if (meAi.data) {
      setProvider(meAi.data.provider);
      aiForm.reset({ model: meAi.data.model, baseUrl: meAi.data.baseUrl ?? "" });
    }
  }, [meAi.data, aiForm]);

  useEffect(() => {
    const models = modelsByProvider[provider] ?? [];
    const current = aiForm.getValues("model");
    if (models.length > 0 && !models.includes(current))
      aiForm.setValue("model", models[0], { shouldValidate: true });
  }, [modelsByProvider, provider, aiForm]);

  const patPrefix = tokens.data?.sourceCraftTokenPrefix;
  const aiPrefix = tokens.data?.aiTokenPrefix;
  const selectedModels = modelsByProvider[provider] ?? [];
  const selectedModel = aiForm.watch("model");

  const handleProviderChange = (value: AiProvider) => {
    setProvider(value);
    const models = modelsByProvider[value] ?? [];
    aiForm.setValue("model", models[0] ?? "", { shouldValidate: true });
  };

  const openPatDialog = () => {
    storeToken.reset();
    setPatDialogOpen(true);
  };

  const openAiTokenDialog = () => {
    storeAi.reset();
    setAiTokenDialogOpen(true);
  };

  const submitPat = (token: string) =>
    storeToken.mutate(token, { onSuccess: () => setPatDialogOpen(false) });

  const saveAiSettings = aiForm.handleSubmit((values) =>
    storeAi.mutate({ provider, model: values.model, token: null, baseUrl: values.baseUrl || null })
  );

  const saveAiToken = (token: string) =>
    storeAi.mutate(
      { provider, model: aiForm.getValues("model"), token, baseUrl: aiForm.getValues("baseUrl") || null },
      { onSuccess: () => setAiTokenDialogOpen(false) }
    );

  return (
    <div className="stack">
      <Card className="card stack">
        <div style={{ fontWeight: 600, display: "flex", alignItems: "center", gap: "0.4rem" }}>
          <Key24Regular /> PAT SourceCraft
        </div>
        <p className="muted" style={{ margin: 0 }}>
          Нужен, чтобы видеть и анализировать ваши (в т.ч. приватные) репозитории. Хранится зашифрованно и повторно не
          показывается.
        </p>
        <div className="row" style={{ alignItems: "center" }}>
          <span className="muted">Текущий токен:</span>
          <code>{patPrefix ?? "не задан"}</code>
          <Button appearance={patPrefix ? "secondary" : "primary"} onClick={openPatDialog}>
            {patPrefix ? "Заменить" : "Добавить PAT"}
          </Button>
        </div>
      </Card>

      <Card className="card stack">
        <div style={{ fontWeight: 600, display: "flex", alignItems: "center", gap: "0.4rem" }}>
          <Sparkle24Regular /> Настройки ИИ
        </div>
        <p className="muted" style={{ margin: 0 }}>
          Выберите провайдера и модель, затем добавьте токен провайдера. Токен хранится зашифрованно и повторно не
          показывается.
        </p>
        <form onSubmit={saveAiSettings}>
          <div className="row" style={{ alignItems: "flex-end" }}>
            <Field label="Провайдер">
              <Dropdown
                selectedOptions={[provider]}
                value={providerLabels[provider]}
                onOptionSelect={(_event, data) => handleProviderChange(data.optionValue as AiProvider)}
              >
                {providers.map((item) => (
                  <Option key={item} value={item}>
                    {providerLabels[item]}
                  </Option>
                ))}
              </Dropdown>
            </Field>
            <Field label="Модель" validationMessage={aiForm.formState.errors.model?.message}>
              <Dropdown
                selectedOptions={selectedModel ? [selectedModel] : []}
                value={selectedModel}
                disabled={selectedModels.length === 0}
                onOptionSelect={(_event, data) =>
                  aiForm.setValue("model", data.optionValue as string, { shouldValidate: true })
                }
              >
                {selectedModels.map((item) => (
                  <Option key={item} value={item}>
                    {item}
                  </Option>
                ))}
              </Dropdown>
            </Field>
            <Field label="Токен провайдера">
              <div className="row" style={{ alignItems: "center", gap: "0.5rem" }}>
                <code>{aiPrefix ?? "не задан"}</code>
                <Button
                  type="button"
                  appearance={aiPrefix ? "secondary" : "primary"}
                  onClick={openAiTokenDialog}
                >
                  {aiPrefix ? "Заменить" : "Добавить токен"}
                </Button>
              </div>
            </Field>
            <Field label="Base URL (необязательно)">
              <Input {...aiForm.register("baseUrl")} />
            </Field>
            <Button type="submit" appearance="primary" icon={<Save24Regular />} disabled={storeAi.isPending}>
              Сохранить
            </Button>
            <Button
              type="button"
              appearance="secondary"
              icon={<Play24Regular />}
              onClick={() => testAi.mutate()}
              disabled={testAi.isPending}
            >
              Тестовый запрос
            </Button>
          </div>
        </form>
        {storeAi.isSuccess ? <span className="tone-good">Настройки ИИ сохранены.</span> : null}
        {testAi.isSuccess && testAi.data ? (
          <span className={testAi.data.ok ? "tone-good" : "tone-bad"}>
            {testAi.data.ok ? `Ответ модели: ${testAi.data.message}` : testAi.data.message}
          </span>
        ) : null}
        {testAi.isError ? <span className="tone-bad">Не удалось выполнить тестовый запрос.</span> : null}
      </Card>

      <SecretDialog
        open={patDialogOpen}
        onOpenChange={setPatDialogOpen}
        title={patPrefix ? "Заменить PAT SourceCraft" : "Добавить PAT SourceCraft"}
        description="Введите новый персональный токен SourceCraft. Он хранится зашифрованно и повторно не показывается."
        fieldLabel="Новый PAT"
        placeholder="pv1_…"
        pending={storeToken.isPending}
        isError={storeToken.isError}
        errorMessage="Не удалось сохранить токен. Проверьте и повторите."
        onSubmit={submitPat}
      >
        <Accordion collapsible>
          <AccordionItem value="how-to-get-pat">
            <AccordionHeader>Как получить PAT?</AccordionHeader>
            <AccordionPanel>
              <ol style={{ margin: "0.25rem 0 0.5rem 1.1rem", padding: 0, lineHeight: 1.7 }}>
                <li>
                  Откройте{" "}
                  <Link href="https://sourcecraft.dev/" target="_blank">
                    sourcecraft.dev
                  </Link>
                  .
                </li>
                <li>
                  Слева откройте <b>Домой</b>, затем <b>Доступ</b>, затем <b>Персональные токены доступа</b>.
                </li>
                <li>Нажмите <b>«Сгенерировать новый токен»</b> и задайте имя (и доступы, если нужно).</li>
                <li>Скопируйте токен и вставьте его в поле выше.</li>
              </ol>
              <p className="muted" style={{ margin: 0 }}>
                Подробнее в документации:{" "}
                <Link href="https://sourcecraft.dev/portal/docs/ru/sourcecraft/security/pat" target="_blank">
                  Получить доступ к репозиторию с помощью PAT
                </Link>
              </p>
            </AccordionPanel>
          </AccordionItem>
        </Accordion>
      </SecretDialog>

      <SecretDialog
        open={aiTokenDialogOpen}
        onOpenChange={setAiTokenDialogOpen}
        title={aiPrefix ? "Заменить токен провайдера" : "Добавить токен провайдера"}
        description={`Введите API-токен для провайдера «${providerLabels[provider]}». Он хранится зашифрованно и повторно не показывается.`}
        fieldLabel="API-токен"
        placeholder="sk-…"
        pending={storeAi.isPending}
        isError={storeAi.isError}
        errorMessage="Не удалось сохранить токен. Проверьте и повторите."
        onSubmit={saveAiToken}
      />
    </div>
  );
}

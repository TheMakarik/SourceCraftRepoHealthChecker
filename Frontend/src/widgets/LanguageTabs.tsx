import { useEffect, useRef, useState } from "react";
import { languageDisplayName } from "../shared/api/labels";

const LONG_PRESS_MS = 300;

export interface LanguageTabsProps {
  languages: string[];
  selected: string[];
  onSelectionChange: (languages: string[]) => void;
}

export function LanguageTabs({ languages, selected, onSelectionChange }: LanguageTabsProps) {
  const [multiSelect, setMultiSelect] = useState(false);
  const multiSelectRef = useRef(false);
  const scrollRef = useRef<HTMLDivElement | null>(null);
  const timerRef = useRef<number | null>(null);
  const longPressRef = useRef(false);

  useEffect(() => {
    const node = scrollRef.current;
    if (!node)
      return;
    const onWheel = (event: WheelEvent) => {
      if (Math.abs(event.deltaY) <= Math.abs(event.deltaX))
        return;
      node.scrollLeft += event.deltaY;
      event.preventDefault();
    };
    node.addEventListener("wheel", onWheel, { passive: false });
    return () => node.removeEventListener("wheel", onWheel);
  }, []);

  useEffect(
    () => () => {
      if (timerRef.current !== null)
        window.clearTimeout(timerRef.current);
    },
    []
  );

  const enableMultiSelect = () => {
    multiSelectRef.current = true;
    setMultiSelect(true);
  };

  const resetMultiSelect = () => {
    multiSelectRef.current = false;
    setMultiSelect(false);
  };

  const cancelLongPress = () => {
    if (timerRef.current !== null) {
      window.clearTimeout(timerRef.current);
      timerRef.current = null;
    }
  };

  const startLongPress = (language: string | null) => {
    longPressRef.current = false;
    cancelLongPress();
    timerRef.current = window.setTimeout(() => {
      longPressRef.current = true;
      enableMultiSelect();
      if (language && !selected.includes(language))
        onSelectionChange([...selected, language]);
    }, LONG_PRESS_MS);
  };

  const allSelected = selected.length === 0;

  const toggleLanguage = (language: string) => {
    onSelectionChange(
      selected.includes(language) ? selected.filter((item) => item !== language) : [...selected, language]
    );
  };

  const activate = (language: string | null, additive: boolean) => {
    if (longPressRef.current) {
      longPressRef.current = false;
      return;
    }
    if (language === null) {
      resetMultiSelect();
      onSelectionChange([]);
      return;
    }

    if (additive)
      enableMultiSelect();
    if (additive || multiSelectRef.current)
      toggleLanguage(language);
    else
      onSelectionChange([language]);
  };

  const pressHandlers = (language: string | null) => ({
    onPointerDown: () => startLongPress(language),
    onPointerUp: cancelLongPress,
    onPointerLeave: cancelLongPress,
    onPointerCancel: cancelLongPress
  });

  const clickHandlers = (language: string | null) => ({
    onClick: (event: React.MouseEvent) => activate(language, event.ctrlKey || event.metaKey)
  });

  return (
    <div
      className={`lang-tabs${multiSelect ? " lang-tabs--multi" : ""}`}
      ref={scrollRef}
      role="tablist"
      aria-label="Фильтр по языку"
      title={multiSelect ? "Мультивыбор: клик переключает языки. «Все» сбрасывает." : "Ctrl/Cmd + клик или долгое нажатие — мультивыбор языков"}
    >
      <button
        type="button"
        role="tab"
        aria-selected={allSelected}
        className={`lang-tab${allSelected ? " lang-tab--active" : ""}`}
        onContextMenu={(event) => event.preventDefault()}
        {...pressHandlers(null)}
        {...clickHandlers(null)}
      >
        Все
      </button>
      {languages.map((language) => {
        const isSelected = selected.includes(language);
        const isPressed = multiSelect && isSelected;
        return (
          <button
            key={language}
            type="button"
            role="tab"
            aria-selected={isSelected}
            className={`lang-tab${isSelected && !multiSelect ? " lang-tab--active" : ""}${
              isPressed ? " lang-tab--pressed" : ""
            }`}
            onContextMenu={(event) => event.preventDefault()}
            {...pressHandlers(language)}
            {...clickHandlers(language)}
          >
            {languageDisplayName(language)}
          </button>
        );
      })}
    </div>
  );
}

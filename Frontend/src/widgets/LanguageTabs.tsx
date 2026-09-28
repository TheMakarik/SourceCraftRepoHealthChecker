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
      setMultiSelect(true);
      if (language && !selected.includes(language))
        onSelectionChange([...selected, language]);
    }, LONG_PRESS_MS);
  };

  const allSelected = selected.length === 0;

  const activateAll = () => {
    setMultiSelect(false);
    onSelectionChange([]);
  };

  const toggleLanguage = (language: string) => {
    if (!multiSelect) {
      onSelectionChange([language]);
      return;
    }
    onSelectionChange(
      selected.includes(language) ? selected.filter((item) => item !== language) : [...selected, language]
    );
  };

  const activate = (language: string | null) => {
    if (longPressRef.current) {
      longPressRef.current = false;
      return;
    }
    if (language === null)
      activateAll();
    else
      toggleLanguage(language);
  };

  const pressHandlers = (language: string | null) => ({
    onPointerDown: () => startLongPress(language),
    onPointerUp: cancelLongPress,
    onPointerLeave: cancelLongPress,
    onPointerCancel: cancelLongPress
  });

  return (
    <div
      className={`lang-tabs${multiSelect ? " lang-tabs--multi" : ""}`}
      ref={scrollRef}
      role="tablist"
      aria-label="Фильтр по языку"
      title={multiSelect ? "Мультивыбор: клик переключает языки. «Все» сбрасывает." : "Долгое нажатие на язык включает мультивыбор"}
    >
      <button
        type="button"
        role="tab"
        aria-selected={allSelected}
        className={`lang-tab${allSelected ? " lang-tab--active" : ""}`}
        onContextMenu={(event) => event.preventDefault()}
        {...pressHandlers(null)}
        onClick={() => activate(null)}
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
            onClick={() => activate(language)}
          >
            {languageDisplayName(language)}
          </button>
        );
      })}
    </div>
  );
}

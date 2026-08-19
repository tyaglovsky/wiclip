# -*- coding: utf-8 -*-
"""Regenerates Resources/Strings.resx, Strings.ru.resx and Strings.cs from one table.

Run from the repository root:  python3 tools/gen-strings.py
"""
import xml.sax.saxutils as sx

# key: (english, russian, comment)
S = [
    # --- трей и общее ---
    ("TrayTooltip",        "WiClip - clipboard history ({0})", "WiClip - история буфера обмена ({0})", "{0} = hotkey"),
    ("MenuOpen",           "Open history\t{0}",                "Открыть историю\t{0}", "{0} = hotkey"),
    ("MenuClear",          "Clear history",                    "Очистить историю", ""),
    ("MenuSettings",       "Settings...",                      "Настройки…", ""),
    ("MenuExit",           "Exit",                             "Выход", ""),
    ("ClearConfirm",       "Delete the whole clipboard history? Pinned entries are kept.",
                           "Удалить всю историю буфера обмена? Закреплённые записи останутся.", ""),
    ("BalloonStarted",     "Clipboard history is running. Press {0} to open it.",
                           "История буфера обмена включена. Вызов: {0}", "{0} = hotkey"),

    # --- окно истории ---
    ("WindowTitle",        "Clipboard",                        "Буфер обмена", ""),
    ("SearchPlaceholder",  "Search history...",                "Поиск по истории…", ""),
    ("TooltipSettings",    "Settings",                         "Настройки", ""),
    ("TooltipClose",       "Close (Esc)",                      "Закрыть (Esc)", ""),
    ("EmptyHistory",       "Nothing here yet. Copy something and it will show up.",
                           "Пока пусто. Скопируйте что-нибудь — запись появится здесь.", ""),
    ("EmptySearch",        "Nothing found.",                   "Ничего не найдено.", ""),
    ("Hints",              "Click - copy · Enter - paste · Alt+1...9 - quick pick · Ctrl+P - pin · Ctrl+S - to library · Shift+Delete - remove · Tab - switch tab",
                           "Клик — скопировать · Enter — вставить · Alt+1…9 — быстрый выбор · Ctrl+P — закрепить · Ctrl+S — в библиотеку · Shift+Delete — удалить · Tab — вкладка", ""),
    ("ToastCopied",        "✓ Copied to clipboard",        "✓ Скопировано в буфер", ""),
    ("ToastCopyFailed",    "Could not copy - the clipboard is busy",
                           "Не удалось скопировать — буфер занят", ""),

    # --- записи ---
    ("PreviewImage",       "\U0001F5BC  Image",                "🖼  Изображение", ""),
    ("PreviewFilesMany",   "\U0001F4C4  Files: {0}",           "📄  Файлов: {0}", "{0} = count"),
    ("ImageSize",          "Image {0}×{1}",               "Изображение {0}×{1}", "{0}x{1} = pixels"),
    ("MetaChars",          "{0} chars",                        "{0} симв.", ""),
    ("TimeJustNow",        "just now",                         "только что", ""),
    ("TimeMinutes",        "{0} min ago",                      "{0} мин назад", ""),
    ("TimeHours",          "{0} h ago",                        "{0} ч назад", ""),
    ("TimeDays",           "{0} d ago",                        "{0} дн назад", ""),

    # --- настройки ---
    ("SettingsTitle",      "WiClip settings",                  "Настройки WiClip", ""),
    ("LabelHotKey",        "Hotkey",                           "Горячая клавиша", ""),
    ("HintHotKey",         "For example: Ctrl+Shift+V, Alt+`, Win+Shift+C",
                           "Например: Ctrl+Shift+V, Alt+`, Win+Shift+C", ""),
    ("LabelMaxItems",      "How many entries to keep",         "Сколько записей хранить", ""),
    ("LabelTheme",         "Appearance",                       "Оформление", ""),
    ("ThemeAuto",          "Match system",                     "Как в системе", ""),
    ("ThemeLight",         "Light",                            "Светлое", ""),
    ("ThemeDark",          "Dark",                             "Тёмное", ""),
    ("LabelLanguage",      "Language",                         "Язык", ""),
    ("LanguageAuto",       "Match system",                     "Как в системе", ""),
    ("CheckAutostart",     "Start with Windows",               "Запускать при входе в Windows", ""),
    ("CheckAutostartByInstaller", "Start with Windows (set by the installer for all users)",
                           "Запускать при входе в Windows (задано установщиком для всех)", ""),
    ("CheckPersist",       "Keep history between sessions",    "Сохранять историю между запусками", ""),
    ("CheckImages",        "Remember images",                  "Запоминать изображения", ""),
    ("CheckPaste",         "Paste the picked entry into the active window",
                           "Сразу вставлять выбранное в активное окно", ""),
    ("CheckSecret",        "Ignore clipboard from password managers",
                           "Не запоминать буфер менеджеров паролей", ""),
    ("LabelIgnored",       "Do not record copies from these processes (comma separated)",
                           "Не запоминать копирование из процессов (через запятую)", ""),
    ("ButtonDataFolder",   "Data folder",                      "Папка с данными", ""),
    ("ButtonCancel",       "Cancel",                           "Отмена", ""),
    ("ButtonSave",         "Save",                             "Сохранить", ""),
    ("LanguageRestartNote","The language applies to windows opened from now on.",
                           "Язык применится к окнам, открытым после сохранения.", ""),

    # --- библиотека ---
    ("TabHistory",         "History",                          "История", ""),
    ("TabLibrary",         "Library",                          "Библиотека", ""),
    ("LibraryEmpty",       "The library is empty. Add text or files - they stay here until you delete them.",
                           "Библиотека пуста. Добавьте текст или файлы — они останутся здесь, пока вы их не удалите.", ""),
    ("LibraryHints",       "Click - copy · Enter - paste · Alt+1...9 - quick pick · F2 - edit · Shift+Delete - remove · Tab - switch tab",
                           "Клик — скопировать · Enter — вставить · Alt+1…9 — быстрый выбор · F2 — изменить · Shift+Delete — удалить · Tab — вкладка", ""),
    ("ButtonAddText",      "+ Text",                           "+ Текст", ""),
    ("ButtonAddFile",      "+ File",                           "+ Файл", ""),
    ("ButtonAddFolder",    "+ Folder",                         "+ Папка", ""),
    ("FolderAll",          "All",                              "Все", ""),
    ("FolderDefault",      "General",                          "Общее", ""),
    ("FolderNew",          "New folder",                       "Новая папка", ""),
    ("SaveToLibrary",      "Save to library (Ctrl+S)",         "Сохранить в библиотеку (Ctrl+S)", ""),
    ("ToastSavedToLibrary","✓ Saved to the library",       "✓ Сохранено в библиотеку", ""),
    ("EditorTitleNew",     "New entry",                        "Новая запись", ""),
    ("EditorTitleEdit",    "Edit entry",                       "Изменить запись", ""),
    ("LabelName",          "Name",                             "Название", ""),
    ("LabelText",          "Text",                             "Текст", ""),
    ("LabelFolder",        "Folder",                           "Папка", ""),
    ("HintNameOptional",   "Leave empty to use the beginning of the text",
                           "Оставьте пустым — подставится начало текста", ""),
    ("PickFilesTitle",     "Add files to the library",         "Добавить файлы в библиотеку", ""),
    ("ConfirmDeleteFolder","Delete the folder \"{0}\" together with all its entries?",
                           "Удалить папку «{0}» вместе со всеми записями?", "{0} = folder name"),
    ("ConfirmDeleteItem",  "Delete \"{0}\" from the library?",
                           "Удалить «{0}» из библиотеки?", "{0} = entry name"),
    ("TooltipPin",         "Keep the window open",             "Не закрывать окно", ""),
    ("TooltipAddText",     "Add a text entry",                 "Добавить текстовую запись", ""),
    ("TooltipAddFile",     "Copy files into the library",      "Скопировать файлы в библиотеку", ""),
    ("TooltipAddFolder",   "Create a folder",                  "Создать папку", ""),
    ("TooltipRenameFolder","Rename the folder (F2)",           "Переименовать папку (F2)", ""),
    ("TooltipDeleteFolder","Delete the folder",                "Удалить папку", ""),
    ("MetaFiles",          "{0} files",                        "файлов: {0}", ""),
    ("MetaFile",           "file",                             "файл", ""),
    ("MetaText",           "text",                             "текст", ""),
    ("MetaImage",          "image",                            "изображение", ""),
    ("DropHint",           "Drop files here to add them to the library",
                           "Перетащите файлы сюда, чтобы добавить их в библиотеку", ""),

    # --- ошибки ---
    ("ErrHotKeyNotSet",    "No shortcut specified.",           "Сочетание не задано.", ""),
    ("ErrHotKeyNoKey",     "The shortcut has no main key.",    "В сочетании нет основной клавиши.", ""),
    ("ErrHotKeyNoModifier","At least one modifier is required (Ctrl, Alt, Shift or Win).",
                           "Нужен хотя бы один модификатор (Ctrl, Alt, Shift или Win).", ""),
    ("ErrHotKeyUnknownKey","Could not recognise the key \"{0}\".",
                           "Не удалось распознать клавишу «{0}».", ""),
    ("ErrHotKeyUnsupported","The key \"{0}\" is not supported.",
                           "Клавиша «{0}» не поддерживается.", ""),
    ("ErrHotKeyTaken",     "The shortcut \"{0}\" is already taken by another program. Pick a different one in WiClip settings.",
                           "Сочетание «{0}» уже занято другой программой. Выберите другое в настройках WiClip.", ""),
    ("ErrHotKeyInvalid",   "Invalid shortcut.",                "Некорректное сочетание клавиш.", ""),
    ("ErrMaxItems",        "The number of entries must be between 1 and 10000.",
                           "Количество записей должно быть числом от 1 до 10000.", ""),
    ("ErrEmptyEntry",      "Enter some text or pick a file.",  "Введите текст или выберите файл.", ""),
    ("ErrFileTooBig",      "\"{0}\" is larger than {1} MB and was not added.",
                           "«{0}» больше {1} МБ — файл не добавлен.", "{0} = file name, {1} = limit"),
    ("ErrCopyFile",        "Could not copy \"{0}\": {1}",
                           "Не удалось скопировать «{0}»: {1}", "{0} = file, {1} = reason"),
    ("ErrFileMissing",     "The stored file is missing.",      "Сохранённый файл потерян.", ""),
]

HEADER = '''<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="metadata">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" />
              </xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="assembly">
            <xsd:complexType>
              <xsd:attribute name="alias" type="xsd:string" />
              <xsd:attribute name="name" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
'''

def write_resx(path, index):
    out = [HEADER]
    for item in S:
        key, comment = item[0], item[3]
        value = item[index]
        out.append('  <data name="%s" xml:space="preserve">\n    <value>%s</value>\n'
                   % (key, sx.escape(value)))
        if comment:
            out.append('    <comment>%s</comment>\n' % sx.escape(comment))
        out.append('  </data>\n')
    out.append('</root>\n')
    open(path, 'w', encoding='utf-8').write(''.join(out))

write_resx('src/WiClip/Resources/Strings.resx', 1)
write_resx('src/WiClip/Resources/Strings.ru.resx', 2)

cs = ['''using System.Globalization;
using System.Resources;

namespace WiClip;

/// <summary>
/// Localised UI strings. Generated from Resources/Strings.resx - do not edit by hand,
/// see tools note in README. English is the neutral language, ru is a satellite assembly.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("WiClip.Resources.Strings", typeof(Strings).Assembly);

    private static bool _resourcesBroken;

    /// <summary>
    /// Looks a string up in the current UI culture. Never throws: a missing or broken
    /// resource must not take the whole application down, the key is shown instead.
    /// </summary>
    public static string Get(string key)
    {
        if (_resourcesBroken) return key;
        try
        {
            return Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        }
        catch (Exception ex)
        {
            _resourcesBroken = true;
            Log.Error($"String resources are unavailable ({ex.GetType().Name}: {ex.Message}). " +
                      "Falling back to resource keys.");
            return key;
        }
    }

    /// <summary>Same, but formatted with the current UI culture.</summary>
    public static string Format(string key, params object[] args)
    {
        try
        {
            return string.Format(CultureInfo.CurrentUICulture, Get(key), args);
        }
        catch (FormatException ex)
        {
            Log.Warn($"Bad format string for '{key}': {ex.Message}");
            return Get(key);
        }
    }
''']
for item in S:
    key = item[0]
    en = item[1].replace('\t', ' ')
    cs.append('\n    /// <summary>%s</summary>\n    public static string %s => Get("%s");\n'
              % (sx.escape(en), key, key))
cs.append('}\n')
open('src/WiClip/Resources/Strings.cs', 'w', encoding='utf-8').write(''.join(cs))

print('ключей:', len(S))

import os
import traceback
import sys

# Папка где находится сам скрипт
root_dir = os.path.dirname(os.path.abspath(__file__))

# Файл structure.txt будет создан там же
output_file = os.path.join(root_dir, "structure.txt")

try:
    print("Проверка пути...")
    
    if not os.path.exists(root_dir):
        print("ОШИБКА: Папка не существует")
        input("Нажми Enter чтобы закрыть...")
        sys.exit(1)

    print("Путь существует")
    print("Начинаю сканирование...")

    with open(output_file, "w", encoding="utf-8") as f:
        for root, dirs, files in os.walk(root_dir):
            level = root.replace(root_dir, '').count(os.sep)
            indent = '    ' * level
            
            f.write(f"{indent}{os.path.basename(root)}/\n")
            
            subindent = '    ' * (level + 1)
            for file in files:
                # чтобы сам structure.txt не попадал в список
                if file != "structure.txt":
                    f.write(f"{subindent}{file}\n")

    print("Готово")
    print("Файл создан:", output_file)

    # Успешное завершение — окно закрывается сразу
    sys.exit(0)

except Exception as e:
    print("ОШИБКА:")
    print(e)
    traceback.print_exc()

print()
print("Скрипт завершён")

# Пауза только если была ошибка
while True:
    try:
        input("Нажми Enter чтобы закрыть...")
        break
    except:
        pass
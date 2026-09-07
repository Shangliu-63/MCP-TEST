import json
import sqlite3
import unittest
from pathlib import Path


PROJECT_DIR = Path(__file__).resolve().parents[1]
DB_PATH = PROJECT_DIR / "data" / "school.db"


class DatabaseTests(unittest.TestCase):
    def setUp(self) -> None:
        self.connection = sqlite3.connect(DB_PATH)

    def tearDown(self) -> None:
        self.connection.close()

    def test_seed_counts(self) -> None:
        expected = {
            "departments": 10,
            "people": 30,
            "responsibilities": 60,
            "test_documents": 20,
            "dispatch_history": 5,
        }
        for table, count in expected.items():
            actual = self.connection.execute(f"SELECT COUNT(*) FROM {table}").fetchone()[0]
            self.assertEqual(count, actual, table)

    def test_foreign_keys(self) -> None:
        self.assertEqual([], self.connection.execute("PRAGMA foreign_key_check").fetchall())

    def test_responsibility_view(self) -> None:
        row = self.connection.execute(
            """
            SELECT department_name, person_name
            FROM responsibility_search_view
            WHERE keywords LIKE '%資訊安全%'
            """
        ).fetchone()
        self.assertEqual(("圖書資訊服務處", "彭詩涵"), row)

    def test_cc_departments_are_valid_json(self) -> None:
        values = self.connection.execute(
            "SELECT expected_cc_department_ids FROM test_documents"
        ).fetchall()
        for (value,) in values:
            self.assertIsInstance(json.loads(value), list)


if __name__ == "__main__":
    unittest.main()

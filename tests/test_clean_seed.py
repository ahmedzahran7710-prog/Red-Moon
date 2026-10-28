import sys
from pathlib import Path
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
from prepare_runtime_data import clean_dump, validate_schema_scope, REFERENCE_TABLES

class CleanSeedTests(unittest.TestCase):
    def test_player_values_removed_but_schema_and_routines_kept(self):
        text = "CREATE TABLE `cq_user` (`id` int);\nINSERT INTO `cq_user` VALUES ('7');\nINSERT INTO `cq_itemtype` VALUES ('8');\n\tINSERT INTO cq_deluser SELECT * FROM cq_user;\n"
        out, kept, removed = clean_dump(text, REFERENCE_TABLES)
        self.assertNotIn("VALUES ('7')", out)
        self.assertIn('CREATE TABLE', out)
        self.assertIn('INSERT INTO cq_deluser SELECT', out)
        self.assertEqual(removed, {'cq_user': 1})
        self.assertEqual(kept, {'cq_itemtype': 1})
    def test_unknown_table_defaults_to_empty(self):
        out, kept, removed = clean_dump("INSERT INTO `new_player_table` VALUES ('1');\n", REFERENCE_TABLES)
        self.assertEqual(out, '')
    def test_profanity_reference_not_mistaken_for_player_state(self):
        self.assertIn('cq_unlawful', REFERENCE_TABLES)
        self.assertNotIn('cq_item', REFERENCE_TABLES)
        self.assertIn('cq_itemtype', REFERENCE_TABLES)
    def test_unrecognized_insert_stops_redaction(self):
        with self.assertRaises(ValueError):
            clean_dump("INSERT INTO `account` (id) VALUES (1);\n", set())
    def test_multiline_seed_stops_redaction(self):
        with self.assertRaises(ValueError):
            clean_dump("INSERT INTO `account` VALUES (\n'1');\n", set())

    def test_no_cross_schema_optional_patch(self):
        for sql in ("UPDATE zf.cq_action SET param='test';", "USE zf;", "DROP DATABASE zf;"):
            with self.subTest(sql=sql), self.assertRaises(ValueError): validate_schema_scope(sql)
        validate_schema_scope("UPDATE `cq_action` SET `param`='test' WHERE id=102;")

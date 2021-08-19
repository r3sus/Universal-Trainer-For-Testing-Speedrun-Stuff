using Flying47.Structs;
using System;
using System.IO;
using System.Windows.Forms;
using System.Linq; // for cast
//using System.Collections.Generic; // handy list operations

namespace Flying47
{
	public partial class PositionsListForm : Form
	{
		public Structs.PositionSets Positions { get; set; }
		private readonly MainForm parent;
		bool ContentChanged = false;

		public PositionsListForm(MainForm parent, Structs.PositionSets Positions)
		{
			this.parent = parent;
			this.Positions = Positions;
			InitializeComponent();
		}

		private int l0=0, t0=0;
		private void PositionsListForm_Move(object sender, EventArgs e)
		{
			if (ChBdock.Checked && l0*t0!=0)
			{
				int dl = Left - l0;
				int dt = Top - t0;
				parent.Left += dl;
				parent.Top += dt;
			}
			l0 = Left; t0 = Top;
		}

		private void PositionsListForm_Load(object sender, EventArgs e)
		{
			EmbedDataInGrid(Positions);
		}

		private void EmbedDataInGrid(PositionSets positions)
		{
			positionGrid.Rows.Clear();
			for (int i = 0; i < positions.PositionsList.Count; i++)
			{
				var elementToAdd = positions.PositionsList[i];
				positionGrid.Rows.Add();
				positionGrid[0, i].Value = elementToAdd.Name;
				positionGrid["X", i].Value = elementToAdd.X.ToString();
				positionGrid["Y", i].Value = elementToAdd.Y.ToString();
				positionGrid["Z", i].Value = elementToAdd.Z.ToString();
			}
			ContentChanged = false;

		}

		private void B_Add_Click(object sender, EventArgs e)
		{
			int rowID=0;
			if (positionGrid.SelectedCells.Count>0) {
				rowID = Math.Min(this.positionGrid.SelectedCells[0].RowIndex+1,positionGrid.RowCount-1);
				this.positionGrid.Rows.Insert(rowID, 1);
			}
			positionGrid[0, rowID].Value = "";
			positionGrid["X", rowID].Value = parent.storedCoordinates.X.ToString("0.00");
			positionGrid["Y", rowID].Value = parent.storedCoordinates.Y.ToString("0.00");
			positionGrid["Z", rowID].Value = parent.storedCoordinates.Z.ToString("0.00");
			ContentChanged = true;
		}

		private int[] xRows()
		{
			var q = from c in positionGrid.SelectedCells.Cast<DataGridViewCell>()
					orderby c.RowIndex descending
					select c.RowIndex;

			return q.Distinct().ToArray();
		}

		public DataGridViewRow CloneWithValues(DataGridViewRow row)
		{
			DataGridViewRow clonedRow = (DataGridViewRow)row.Clone();
			for (Int32 index = 0; index < row.Cells.Count; index++)
			{
				clonedRow.Cells[index].Value = row.Cells[index].Value;
			}
			return clonedRow;
		}

		private void btnDupe_Click(object sender, EventArgs e)
		{
			foreach (int rowID in xRows())
			{
				if (rowID >= positionGrid.Rows.Count) continue;
				var dr = CloneWithValues(positionGrid.Rows[rowID]);
				positionGrid.Rows.Insert(rowID+1, dr);
				ContentChanged = true;
			}
		}

		private void B_RemoveEntry_Click(object sender, EventArgs e)
		{
			foreach (int rowID in xRows())
			{
				if (rowID >= positionGrid.Rows.Count) continue;
				positionGrid.Rows.RemoveAt(rowID);
				ContentChanged = true;
			}
		}

		private void B_TeleportTo_Click(object sender, EventArgs e)
		{
			var selectedCells = this.positionGrid.SelectedCells;
			if (selectedCells.Count > 0)
			{
				var firstCell = selectedCells[0];
				var rowID = firstCell.RowIndex;
				if (rowID < positionGrid.Rows.Count - 1)
				{
					if (IsValidRow(rowID, out float posX, out float posY, out float posZ))
					{
						parent.storedCoordinates.X = posX;
						parent.storedCoordinates.Y = posY;
						parent.storedCoordinates.Z = posZ;
						parent.Load_Position();
					}
				}
			}
		}

		private void PositionGrid_CellLeave(object sender, DataGridViewCellEventArgs e)
		{
			var column = e.ColumnIndex;
			if ((column == 1 || column == 2 || column == 3) && positionGrid.EditingControl != null)
			{
				var validatedValue = positionGrid.EditingControl.Text;
				if (!float.TryParse(validatedValue, out _))
				{
					MessageBox.Show("Failed to parse float value. Replacing with 0", "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
					positionGrid.EditingControl.Text = "0";
				}
			}
		}

		private void B_LoadTable_Click(object sender, EventArgs e)
		{
			FileDialog fd = new OpenFileDialog()
			{
				InitialDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Stored Lists"),
				Filter = "Stored Position List|*.xpos"
			};
			var res = fd.ShowDialog();
			if (res == DialogResult.OK)
			{
				Positions = PositionSets.Load(fd.FileName);
				EmbedDataInGrid(Positions);
			}
		}

		private void B_SaveTable_Click(object sender, EventArgs e)
		{
			if (!Directory.Exists("Stored Lists"))
				Directory.CreateDirectory("Stored Lists");
			FileDialog fd = new SaveFileDialog()
			{
				InitialDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Stored Lists"),
				FileName = parent.processName + ".xpos",
				Filter = "Stored Position List|*.xpos"
			};
			
			string fp = Path.Combine("Stored Lists", parent.processName + ".xpos");
			bool x;
			x = File.Exists(fp);
			if (cbSAS.Checked || !x) {x = fd.ShowDialog() == DialogResult.OK; fp = fd.FileName;}
			if (!x) {return;}

			Structs.PositionSets positions = new Structs.PositionSets();
			for (int i = 0; i < positionGrid.Rows.Count - 1; i++)
			{
				var posName = GetSafeStringValue(positionGrid[0, i]);
				if (IsValidRow(i, out float posX, out float posY, out float posZ))
				{
					positions.PositionsList.Add(new Structs.PositionSet_Coordinates(posName, posX, posY, posZ));
				}
				else
					return;
			}

			positions.Save(fp);
			this.Positions = positions;
			this.parent.ListOfStoredPositions = positions;
			ContentChanged = false;
		}

		private string GetSafeStringValue(DataGridViewCell cellData)
		{
			if (cellData == null)
				return "";
			else
				return cellData.Value.ToString();
		}


		private bool IsValidRow(int rowIndex, out float posX, out float posY, out float posZ)
		{
			var posXStr = GetSafeStringValue(positionGrid["X", rowIndex]);
			var posYStr = GetSafeStringValue(positionGrid["Y", rowIndex]);
			var posZStr = GetSafeStringValue(positionGrid["Z", rowIndex]);

			bool wasError = false;
			string errorMsg = "Error in row " + (rowIndex + 1).ToString() + ": ";
			if (!float.TryParse(posXStr.ToString(), out posX))
			{
				wasError = true;
				errorMsg += "X is not a valid float value! ";
			}

			if (!float.TryParse(posYStr.ToString(), out posY))
			{
				wasError = true;
				errorMsg += "Y is not a valid float value! ";
			}

			if (!float.TryParse(posZStr.ToString(), out posZ))
			{
				wasError = true;
				errorMsg += "Z is not a valid float value! ";
			}

			if (wasError)
			{
				MessageBox.Show(errorMsg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return false;
			}
			else
			{
				return true;
			}
		}

		private void PositionsListForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (ContentChanged)
			{
				var result = MessageBox.Show("All unsaved changes will be lost, do you want to close the window?", "Question", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk);
				e.Cancel = (result == DialogResult.No);
			}
		}

		private void PositionGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
		{
			ContentChanged = true;
		}

		private void PositionGrid_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
		{
			parent.m_KeyboardHook.KeysEnabled = false;
		}

        private void PositionGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
		{
			parent.m_KeyboardHook.KeysEnabled = true;
		}
	}
}

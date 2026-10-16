
import { NavLink } from 'react-router-dom'
import type { UserRole } from '../routes/roles'
import { roleMenus } from '../routes/roleMenus'

type SidebarProps = {
  userRole: UserRole
}

export default function Sidebar({ userRole }: SidebarProps) {
  const menus = roleMenus[userRole]

  return (
    <aside>
      <h2>AURA</h2>

      <nav>
        {menus.map((menu) => (
          <NavLink key={menu.path} to={menu.path}>
            {menu.label}
          </NavLink>
        ))}
      </nav>
    </aside>
  )
}
